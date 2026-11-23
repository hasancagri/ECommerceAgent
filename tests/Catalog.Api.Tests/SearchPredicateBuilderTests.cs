using Catalog.Api.Search;
using Catalog.Api.Domains.Products.ValueObjects;
using Shouldly;
using Xunit;

namespace Catalog.Api.Tests;

// 018 (İlke VI test-first): SearchRequest → ParadeDB @@@ clause + SQL predicate'leri saf kurar (DB'siz).
// Yapısal filtre + text OR-grupları tek pozitif clause'ta birleşir; clause boşsa null (çağıran all()).
// Canlı kontrat (research.md T010 kilidi): term/range/uuid-quoted pozitif; ExcludePublisher NOT clause'da
// BOZUK → SQL predicate. publisherId/excludePublisherId handler'da Marten ile ad→id çözülüp verilir (saf).
public class SearchPredicateBuilderTests
{
    static readonly Guid Pub = Guid.Parse("8d841f29-7ed7-4eb0-80d9-0fea9bc9d8e2");

    [Fact]
    public void Build_NoFilters_ReturnsMatchAll()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest());
        p.PdbClause.ShouldBeNull();           // çağıran paradedb.all() kullanır
        p.SqlFilters.ShouldBeEmpty();
    }

    // 021: text OR-grubu taksonomi alanlarını da tarar (Genre/Moods/Themes — Tags.Value yerine).
    // Liste alanları `.$values` ($type/$values sarmalayıcı tuzağı — canlı doğrulandı).
    private static string TextGroup(string t) =>
        $"(data.Name:{t} OR data.Description:{t} OR data.Genre:{t} OR data.Moods.$values:{t} OR data.Themes.$values:{t})";

    [Fact]
    public void Build_TextSingleTerm_BuildsOrGroup()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(Text: "Orwell"));
        p.PdbClause.ShouldBe(TextGroup("Orwell"));
    }

    [Fact]
    public void Build_TextMultipleTerms_JoinsWithAnd()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(Text: "George Orwell"));
        p.PdbClause.ShouldBe(TextGroup("George") + " AND " + TextGroup("Orwell"));
    }

    [Fact]
    public void Build_TextFillerOnly_ReturnsMatchAll()
    {
        // Bm25Terms tüm terimleri eler (dolgu/soru) → yapısal filtre de yoksa match-all.
        var p = SearchPredicateBuilder.Build(new SearchRequest(Text: "bana bir kitap misin"));
        p.PdbClause.ShouldBeNull();
    }

    static readonly Guid Pub2 = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void Build_PublisherSingleId_AddsQuotedTerm()
    {
        // Publisher adı handler'da Marten ile id SET'ine çözülür; builder resolved id'leri alır.
        var p = SearchPredicateBuilder.Build(new SearchRequest(Publisher: "YKY"), publisherIds: [Pub]);
        p.PdbClause.ShouldBe($"data.PublisherId:\"{Pub}\"");
    }

    [Fact]
    public void Build_PublisherMultipleIds_AddsOrGroup()
    {
        // Ad Contains birden çok yayıneviyle eşleşirse OR-grubu (sıfır-regresyon).
        var p = SearchPredicateBuilder.Build(new SearchRequest(Publisher: "Yayın"),
            publisherIds: [Pub, Pub2]);
        p.PdbClause.ShouldBe($"(data.PublisherId:\"{Pub}\" OR data.PublisherId:\"{Pub2}\")");
    }

    [Fact]
    public void Build_PriceClosedRange_AddsBetween()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(PriceMin: 100m, PriceMax: 300m));
        p.PdbClause.ShouldBe("data.Price.Amount:[100 TO 300]");
    }

    [Fact]
    public void Build_PriceMinOnly_AddsOpenGte()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(PriceMin: 100m));
        p.PdbClause.ShouldBe("data.Price.Amount:>=100");
    }

    [Fact]
    public void Build_PriceMaxOnly_AddsOpenLte()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(PriceMax: 300m));
        p.PdbClause.ShouldBe("data.Price.Amount:<=300");
    }

    [Fact]
    public void Build_PriceDecimal_UsesInvariantDot()
    {
        // InvariantGlobalization (010) '.' ondalık garantiler — tr-TR virgül SIZMAZ.
        var p = SearchPredicateBuilder.Build(new SearchRequest(PriceMin: 99.9m));
        p.PdbClause.ShouldBe("data.Price.Amount:>=99.9");
    }

    [Fact]
    public void Build_PageRange_AddsPageCountBetween()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(PageMin: 100, PageMax: 300));
        p.PdbClause.ShouldBe("data.PageCount:[100 TO 300]");
    }

    [Fact]
    public void Build_Binding_AddsIntTerm()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(Binding: BindingType.KartonKapak));
        p.PdbClause.ShouldBe("data.Binding:1");
    }

    [Fact]
    public void Build_OnlySalable_AddsSalableTrue()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(OnlySalable: true));
        p.PdbClause.ShouldBe("data.IsSalable:true");
    }

    [Fact]
    public void Build_OnlyDiscounted_AddsDiscountGtZero()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(OnlyDiscounted: true));
        p.PdbClause.ShouldBe("data.DiscountRate:>0");
    }

    [Fact]
    public void Build_ExcludePublisher_GoesToSqlFilterNotClause()
    {
        // NOT string query'de BOZUK (research.md T010) → SQL predicate; clause POZİTİF kalır.
        var p = SearchPredicateBuilder.Build(new SearchRequest(ExcludePublisher: "YKY"),
            excludePublisherIds: [Pub]);
        p.PdbClause.ShouldBeNull();
        p.SqlFilters.Count.ShouldBe(1);
        var f = p.SqlFilters[0];
        f.Column.ShouldBe("data->>'PublisherId'");
        f.Op.ShouldBe("<>");
        f.Value.ShouldBe(Pub.ToString());
    }

    [Fact]
    public void Build_MultipleFilters_JoinWithAndInDeterministicOrder()
    {
        // Sıra: text → publisher → binding → price → page → salable → discounted (021: kategori çıktı).
        var p = SearchPredicateBuilder.Build(
            new SearchRequest(Text: "Orwell", Binding: BindingType.Ciltli,
                PriceMin: 100m, PriceMax: 300m, OnlySalable: true, OnlyDiscounted: true),
            publisherIds: [Pub]);
        p.PdbClause.ShouldBe(
            TextGroup("Orwell") + " AND " +
            $"data.PublisherId:\"{Pub}\" AND " +
            "data.Binding:2 AND " +
            "data.Price.Amount:[100 TO 300] AND " +
            "data.IsSalable:true AND " +
            "data.DiscountRate:>0");
    }

    [Fact]
    public void Build_ExcludePublisherWithPositiveFilters_ClauseAndSqlCoexist()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(OnlySalable: true, ExcludePublisher: "X"),
            excludePublisherIds: [Pub]);
        p.PdbClause.ShouldBe("data.IsSalable:true");
        p.SqlFilters.Count.ShouldBe(1);
        p.SqlFilters[0].Value.ShouldBe(Pub.ToString());
    }

    // --- 021: taksonomi facet'leri jsonb SQL predicate (@@@ DIŞI — token çarpışmasını önler) ---

    [Fact]
    public void Build_GenreFacet_AddsAnyFilterNotInClause()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(Genres: ["roman", "öykü"]));
        p.PdbClause.ShouldBeNull(); // facet @@@ clause'a GİRMEZ
        var f = p.SqlFilters.ShouldHaveSingleItem();
        f.Column.ShouldBe("data->>'Genre'");
        f.Op.ShouldBe("= ANY");
        f.Value.ShouldBe(new[] { "roman", "öykü" });
    }

    [Fact]
    public void Build_AudienceLevelFacet_AddsAnyFilter()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(AudienceLevels: ["çocuk (7-10)"]));
        var f = p.SqlFilters.ShouldHaveSingleItem();
        f.Column.ShouldBe("data->>'AudienceLevel'");
        f.Op.ShouldBe("= ANY");
    }

    [Fact]
    public void Build_MoodsFacet_AddsExistsFilterWithOrSemantics()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(Moods: ["karanlık", "gizemli"]));
        var f = p.SqlFilters.ShouldHaveSingleItem();
        f.Column.ShouldBe("data->'Moods'->'$values'"); // $values sarmalayıcı tuzağı (canlı doğrulandı)
        f.Op.ShouldBe("?|"); // jsonb ?| text[] = herhangi biri (OR bedava)
        f.Value.ShouldBe(new[] { "karanlık", "gizemli" });
    }

    [Fact]
    public void Build_ThemesFacet_AddsExistsFilter()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(Themes: ["aşk/romantizm"]));
        var f = p.SqlFilters.ShouldHaveSingleItem();
        f.Column.ShouldBe("data->'Themes'->'$values'");
        f.Op.ShouldBe("?|");
    }

    [Fact]
    public void Build_OnlyInStock_AddsStockCountFilter()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(OnlyInStock: true));
        var f = p.SqlFilters.ShouldHaveSingleItem();
        f.Column.ShouldBe("(data->>'StockCount')::int");
        f.Op.ShouldBe(">");
        f.Value.ShouldBe(0);
    }

    [Fact]
    public void Build_EmptyFacetLists_NoFilters()
    {
        var p = SearchPredicateBuilder.Build(new SearchRequest(
            Genres: [], Moods: [], AudienceLevels: [], Themes: []));
        p.SqlFilters.ShouldBeEmpty();
    }

    [Fact]
    public void Build_MultipleFacetsAndExclude_CoexistInSqlFilters()
    {
        var p = SearchPredicateBuilder.Build(
            new SearchRequest(Genres: ["roman"], Themes: ["kimlik"], OnlyInStock: true,
                ExcludePublisher: "X"),
            excludePublisherIds: [Pub]);
        // ExcludePublisher + Genre(ANY) + Themes(?|) + InStock(>) = 4 SQL predicate; @@@ clause boş.
        p.PdbClause.ShouldBeNull();
        p.SqlFilters.Count.ShouldBe(4);
    }
}
