using Catalog.Api.Domains.Products.Features.Agents.Queries;

namespace Catalog.Api.Tests;

// 021 T033 (test-first, İlke VI): similar_books'un SAF birimleri — BM25 örtüşme sorgu-string kurucu +
// fallback kararı. SQL yürütümü (SimilarBooksQueries) host-gerekli → canlı doğrulanır (quickstart).
public class SimilarBooksQueryTests
{
    // Liste alanları `.$values` ($type/$values sarmalayıcı tuzağı — canlı doğrulandı).
    [Fact]
    public void BuildOverlapQuery_ThemesAndMoods_BuildsOrTerms()
    {
        var q = SimilarBooks.Overlap.BuildBm25Query(
            themes: new[] { "distopya/ütopya", "güç/iktidar" },
            moods: new[] { "karanlık" });

        q.ShouldBe("data.Themes.$values:\"distopya/ütopya\" OR data.Themes.$values:\"güç/iktidar\" OR data.Moods.$values:\"karanlık\"");
    }

    [Fact]
    public void BuildOverlapQuery_OnlyThemes_BuildsThemeTerms()
    {
        var q = SimilarBooks.Overlap.BuildBm25Query(new[] { "aşk/romantizm" }, Array.Empty<string>());
        q.ShouldBe("data.Themes.$values:\"aşk/romantizm\"");
    }

    [Fact]
    public void BuildOverlapQuery_OnlyMoods_BuildsMoodTerms()
    {
        var q = SimilarBooks.Overlap.BuildBm25Query(Array.Empty<string>(), new[] { "mizahi/absürt" });
        q.ShouldBe("data.Moods.$values:\"mizahi/absürt\"");
    }

    [Fact]
    public void BuildOverlapQuery_NoThemesOrMoods_ReturnsNull()
    {
        // Örtüşme sinyali yok → fallback (aynı yazar/tür) devreye girer.
        SimilarBooks.Overlap.BuildBm25Query(Array.Empty<string>(), Array.Empty<string>()).ShouldBeNull();
    }

    [Fact]
    public void BuildOverlapQuery_IgnoresBlankValues()
    {
        var q = SimilarBooks.Overlap.BuildBm25Query(new[] { "aile", "  ", "" }, new[] { "" });
        q.ShouldBe("data.Themes.$values:\"aile\"");
    }

    [Fact]
    public void BuildOverlapQuery_EscapesEmbeddedQuotes()
    {
        // Sözlük değerleri tırnak taşımaz ama savunma: gömülü " kaçırılır (sorgu kırılmaz).
        var q = SimilarBooks.Overlap.BuildBm25Query(new[] { "a\"b" }, Array.Empty<string>());
        q.ShouldBe("data.Themes.$values:\"a\\\"b\"");
    }
}
