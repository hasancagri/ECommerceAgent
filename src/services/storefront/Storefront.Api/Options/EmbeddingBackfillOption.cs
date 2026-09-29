namespace Storefront.Api.Options;

// SemanticSearchOption söküldü (eşik 0.68 prompt kalıbına gömüldü — research R7); backfill'in
// batch ayarı bu dar option'da yaşar (R7'nin ıskaladığı ikinci tüketici EmbeddingBackfillService'ti).
public class EmbeddingBackfillOption
{
    // Backfill'de tek OpenAI isteğine giden metin sayısı (20k katalog ≈ 40 istek → dakikalar).
    [Range(1, 2000)] public int BackfillBatchSize { get; set; } = 500;
}
