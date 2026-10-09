namespace Storefront.Api.Tests;

// 086 (067'den taşındı) İLKE VI: yeniden-embedding kararı — boş→Clear, değişti→Generate,
// aynı+var→Keep, aynı+yok→Generate. Projeksiyon önceki açıklama/vektörü ES'teki mevcut doc'tan okur.
public class EmbeddingDecisionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Description_WhenNewIsEmpty_EmbeddingIsCleared(string? newDescription)
    {
        StorefrontDocument.DecideEmbedding(newDescription, "eski açıklama", hasEmbedding: true)
            .ShouldBe(EmbeddingDecision.Clear);
    }

    [Fact]
    public void Description_UnchangedAndEmbeddingPresent_IsUntouched()
    {
        StorefrontDocument.DecideEmbedding("aynı metin", "aynı metin", hasEmbedding: true)
            .ShouldBe(EmbeddingDecision.Keep);
    }

    [Fact]
    public void Description_UnchangedButEmbeddingAbsent_IsGenerated()
    {
        StorefrontDocument.DecideEmbedding("aynı metin", "aynı metin", hasEmbedding: false)
            .ShouldBe(EmbeddingDecision.Generate);
    }

    [Fact]
    public void Description_Changed_EmbeddingIsRegenerated()
    {
        StorefrontDocument.DecideEmbedding("yeni metin", "eski metin", hasEmbedding: true)
            .ShouldBe(EmbeddingDecision.Generate);
    }

    [Fact]
    public void Description_WhenGoesEmptyToFilled_EmbeddingIsGenerated()
    {
        StorefrontDocument.DecideEmbedding("yeni gelen açıklama", null, hasEmbedding: false)
            .ShouldBe(EmbeddingDecision.Generate);
    }

    [Fact]
    public void Karar_ordinal_case_duyarli()
    {
        StorefrontDocument.DecideEmbedding("Metin", "metin", hasEmbedding: true)
            .ShouldBe(EmbeddingDecision.Generate);
    }
}