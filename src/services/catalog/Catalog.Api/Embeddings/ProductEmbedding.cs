namespace Catalog.Api.Embeddings;

// 096: kitabın anlamsal temsili — Marten dokümanı, aggregate DEĞİL (ProductCatalogRef emsali;
// invariant taşımaz, Domains/ dışı). Kaynağı her zaman Product+Author+Category'den yeniden
// üretilebilir; tek yazar EmbeddingReconcileProcess. Marten TUZAĞI: saklanan tip parametresiz
// private ctor + private setter ister (record VO deserialize dersi).
public class ProductEmbedding
{
    // = ProductId (1:1, doğal anahtar).
    public Guid Id { get; private set; }

    // bge-m3 → 1024 eleman; JSONB içinde yaşar (R2 — vektör index'i bilinçli yok).
    public float[] Vector { get; private set; } = [];

    // SHA256(compose edilmiş metin) — tazelik anahtarı. Model adı hash'e GİRMEZ (ayrı eksen).
    public string ContentHash { get; private set; } = string.Empty;

    // Üreten model kimliği; config modeli değişince tüm kayıtlar bayatlar (FR-006).
    public string Model { get; private set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; private set; }

    private ProductEmbedding()
    {
    }

    /// <summary>Bayatlık kararı (saf): kayıt yok ∨ hash farklı ∨ model farklı → yeniden üret.</summary>
    public static bool NeedsRefresh(string? existingHash, string? existingModel, string currentHash, string currentModel)
        => existingHash != currentHash || existingModel != currentModel;

    public static ProductEmbedding Create(Guid productId, float[] vector, string contentHash, string model)
        => new()
        {
            Id = productId,
            Vector = vector,
            ContentHash = contentHash,
            Model = model,
            UpdatedAtUtc = DateTime.UtcNow,
        };
}
