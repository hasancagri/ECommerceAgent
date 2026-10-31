using Catalog.Api.Embeddings;

namespace Catalog.Api.Tests;

// 096 T007: bayatlık kararı saf çekirdeği (İlke VI test-first). Kural (data-model.md):
// kayıt yok ∨ ContentHash ≠ güncel hash ∨ Model ≠ config modeli → yeniden üret.
public class EmbeddingRefreshDecisionTests
{
    [Fact]
    public void NeedsRefresh_NoExistingRecord_ReturnsTrue()
    {
        ProductEmbedding.NeedsRefresh(null, null, "hash", "bge-m3").ShouldBeTrue();
    }

    [Fact]
    public void NeedsRefresh_DifferentHash_ReturnsTrue()
    {
        ProductEmbedding.NeedsRefresh("eski-hash", "bge-m3", "yeni-hash", "bge-m3").ShouldBeTrue();
    }

    [Fact]
    public void NeedsRefresh_DifferentModel_ReturnsTrue()
    {
        ProductEmbedding.NeedsRefresh("hash", "bge-m3", "hash", "qwen3-embedding").ShouldBeTrue();
    }

    [Fact]
    public void NeedsRefresh_SameHashAndModel_ReturnsFalse()
    {
        ProductEmbedding.NeedsRefresh("hash", "bge-m3", "hash", "bge-m3").ShouldBeFalse();
    }
}
