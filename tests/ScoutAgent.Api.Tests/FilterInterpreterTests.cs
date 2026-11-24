using ScoutAgent.Api.Search;

namespace ScoutAgent.Api.Tests;

// 022 (İlke VI test-first): BookSearchFilters (tipli niyet-slot) → search_books argüman dict.
// Pass-through slotlar + PriceHint/PageHint metin→sayı parse (yön kelimesi); grup-null → anahtar YOK
// (uydurma yasağı). Ambiguous/sayısız hint → filtre düşer (tahmin etme).
public class FilterInterpreterTests
{
    static Dictionary<string, object?> Args(BookSearchFilters f) => FilterInterpreter.ToSearchArgs(f);

    [Fact]
    public void ToSearchArgs_Empty_OnlyPaging()
    {
        var a = Args(new BookSearchFilters());
        a["page"].ShouldBe(1);
        a["pageSize"].ShouldBe(10);
        a.ShouldNotContainKey("author");
        a.ShouldNotContainKey("query");
        a.ShouldNotContainKey("minPrice");
        a.ShouldNotContainKey("sort");
    }

    [Fact]
    public void ToSearchArgs_Author_MapsAuthorNotQuery()
    {
        var a = Args(new BookSearchFilters(Text: new TextSlot(Author: "Mo Yan")));
        a["author"].ShouldBe("Mo Yan");
        a.ShouldNotContainKey("query");
    }

    [Fact]
    public void ToSearchArgs_Query_MapsQuery()
    {
        var a = Args(new BookSearchFilters(Text: new TextSlot(Query: "karanlık distopya")));
        a["query"].ShouldBe("karanlık distopya");
        a.ShouldNotContainKey("author");
    }

    [Fact]
    public void ToSearchArgs_Publisher_MapsIncludeExclude()
    {
        var a = Args(new BookSearchFilters(Publisher: new PublisherSlot(Include: "Can Yayınları", Exclude: "İş Bankası")));
        a["publisher"].ShouldBe("Can Yayınları");
        a["excludePublisher"].ShouldBe("İş Bankası");
    }

    [Fact]
    public void ToSearchArgs_SortAndBindingHint_PassThroughAsText()
    {
        var a = Args(new BookSearchFilters(SortHint: "ucuzdan pahalıya", BindingHint: "ciltli"));
        a["sort"].ShouldBe("ucuzdan pahalıya");   // Catalog ParseSort yorumlar
        a["binding"].ShouldBe("ciltli");          // Catalog binding parse
    }

    [Fact]
    public void ToSearchArgs_Flags_MapBooleans()
    {
        var a = Args(new BookSearchFilters(Flags: new FlagsSlot(OnlyDiscounted: true, OnlyInStock: true, OnlySalable: true)));
        a["onlyDiscounted"].ShouldBe(true);
        a["onlyInStock"].ShouldBe(true);
        a["onlySalable"].ShouldBe(true);
    }

    [Fact]
    public void ToSearchArgs_NoFlags_NoBooleanKeys()
    {
        var a = Args(new BookSearchFilters());
        a.ShouldNotContainKey("onlyDiscounted");
        a.ShouldNotContainKey("onlyInStock");
        a.ShouldNotContainKey("onlySalable");
    }

    // --- PriceHint parse ---

    [Fact]
    public void ToSearchArgs_PriceMaxPhrase_SetsMaxPrice()
    {
        var a = Args(new BookSearchFilters(PriceHint: "300 TL altı"));
        a["maxPrice"].ShouldBe(300m);
        a.ShouldNotContainKey("minPrice");
    }

    [Fact]
    public void ToSearchArgs_PriceRange_SetsMinAndMax()
    {
        var a = Args(new BookSearchFilters(PriceHint: "100-300 TL"));
        a["minPrice"].ShouldBe(100m);
        a["maxPrice"].ShouldBe(300m);
    }

    [Fact]
    public void ToSearchArgs_PriceMinPhrase_SetsMinPrice()
    {
        var a = Args(new BookSearchFilters(PriceHint: "en az 100 TL"));
        a["minPrice"].ShouldBe(100m);
        a.ShouldNotContainKey("maxPrice");
    }

    [Fact]
    public void ToSearchArgs_PriceNoNumber_Dropped()
    {
        var a = Args(new BookSearchFilters(PriceHint: "ucuz"));
        a.ShouldNotContainKey("minPrice");
        a.ShouldNotContainKey("maxPrice");
    }

    [Fact]
    public void ToSearchArgs_PriceBareNumberNoKeyword_Dropped()
    {
        // Tek sayı + yön kelimesi yok → ambiguous → filtre düşer (tahmin etme / uydurma yasağı).
        var a = Args(new BookSearchFilters(PriceHint: "300 TL"));
        a.ShouldNotContainKey("minPrice");
        a.ShouldNotContainKey("maxPrice");
    }

    // --- PageHint parse ---

    [Fact]
    public void ToSearchArgs_PageMinPhrase_SetsMinPageCount()
    {
        var a = Args(new BookSearchFilters(PageHint: "300 sayfadan fazla"));
        a["minPageCount"].ShouldBe(300);
        a.ShouldNotContainKey("maxPageCount");
    }

    [Fact]
    public void ToSearchArgs_PageMaxPhrase_SetsMaxPageCount()
    {
        var a = Args(new BookSearchFilters(PageHint: "en fazla 200 sayfa"));
        a["maxPageCount"].ShouldBe(200);
        a.ShouldNotContainKey("minPageCount");
    }

    [Fact]
    public void ToSearchArgs_PageNoNumber_Dropped()
    {
        var a = Args(new BookSearchFilters(PageHint: "kısa"));
        a.ShouldNotContainKey("minPageCount");
        a.ShouldNotContainKey("maxPageCount");
    }

    // --- paging + taxonomy ---

    [Fact]
    public void ToSearchArgs_PagingCarried()
    {
        var a = Args(new BookSearchFilters(Page: 3, PageSize: 20));
        a["page"].ShouldBe(3);
        a["pageSize"].ShouldBe(20);
    }

    [Fact]
    public void ToSearchArgs_Taxonomy_FoldedIntoQuery()
    {
        // Taksonomi slotu dolduysa kelimeleri query'ye kat (Catalog ExtractFromText facet'e çevirir).
        var a = Args(new BookSearchFilters(
            Text: new TextSlot(Query: "roman"),
            Taxonomy: new TaxonomySlot(Moods: new[] { "karanlık" }, Themes: new[] { "distopya" })));
        ((string)a["query"]!).ShouldContain("roman");
        ((string)a["query"]!).ShouldContain("karanlık");
        ((string)a["query"]!).ShouldContain("distopya");
    }
}
