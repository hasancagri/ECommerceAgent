using ScoutAgent.Api.Search;
using Shouldly;
using Xunit;

namespace ScoutAgent.Api.Tests;

// 026 US2 (İlke VI test-first): semantik niyet-ayrımı SAF. SemanticQuery dolu + rakip DEMİR (yazar/
// yayınevi/benzer-çapa/lexical metin) yoksa → semantik. Demir varsa lexical kazanır.
public class SemanticIntentTests
{
    private static BookSearchFilters Sem(string? q) => new(SemanticQuery: q);

    [Fact]
    public void IsSemantic_OnlySemanticQuery_ReturnsTrue()
    {
        SemanticIntent.IsSemantic(Sem("kayıp üstüne sakin bir kitap")).ShouldBeTrue();
    }

    [Fact]
    public void IsSemantic_EmptySemanticQuery_ReturnsFalse()
    {
        SemanticIntent.IsSemantic(new BookSearchFilters()).ShouldBeFalse();
        SemanticIntent.IsSemantic(Sem("  ")).ShouldBeFalse();
    }

    [Fact]
    public void IsSemantic_WithAuthor_ReturnsFalse()
    {
        SemanticIntent.IsSemantic(new BookSearchFilters(
            Text: new TextSlot(Author: "Dostoyevski"), SemanticQuery: "karanlık")).ShouldBeFalse();
    }

    [Fact]
    public void IsSemantic_WithLeakedTextQuery_ReturnsTrue()
    {
        // LLM betimleyici sorguyu bölüp konu kelimelerini Text.Query'ye sızdırabilir (canlı gözlendi);
        // Text.Query kimlik demiri DEĞİL → semantik yine seçilir (dalda SemanticQuery'ye katlanır).
        SemanticIntent.IsSemantic(new BookSearchFilters(
            Text: new TextSlot(Query: "kayıp, yas"), SemanticQuery: "sakin, melankolik bir roman")).ShouldBeTrue();
    }

    [Fact]
    public void IsSemantic_WithPublisher_ReturnsFalse()
    {
        SemanticIntent.IsSemantic(new BookSearchFilters(
            Publisher: new PublisherSlot(Include: "Can"), SemanticQuery: "karanlık")).ShouldBeFalse();
    }

    [Fact]
    public void IsSemantic_WithSimilarToTitle_ReturnsFalse()
    {
        SemanticIntent.IsSemantic(new BookSearchFilters(
            SimilarToTitle: "Simyacı", SemanticQuery: "karanlık")).ShouldBeFalse();
    }
}
