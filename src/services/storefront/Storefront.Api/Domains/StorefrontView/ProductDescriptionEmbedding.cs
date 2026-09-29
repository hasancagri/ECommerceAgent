namespace Storefront.Api.Domains.StorefrontView;

// aciklamadan turetilen anlamsal temsil (1536, kosinus) — StorefrontView'un PK-es AYRI yol arkadasi
// dokumani. AYRI cunku: 1536 float JSONB'de ~17KB metin eder; view icinde olsaydi tum tam-satir okuma
// yollari (liste/facet/arama) her cagrida yuzlerce MB tasirdi. Yasam-dongusu durumu TASIMAZ: gorunurluk
// HER ZAMAN StorefrontView satilabilirlik filtresinden gelir (FR-007); yazan da ayni handler/transaction.
// Aggregate DEGIL (invariant yok, read-model yardimcisi). Kullaniciya asla gosterilmez.
public class ProductDescriptionEmbedding
{
    private ProductDescriptionEmbedding()
    {
    }

    public Guid ProductId { get; private set; }
    public float[] Vector { get; private set; } = [];

    public static ProductDescriptionEmbedding Create(Guid productId, float[] vector) =>
        new() { ProductId = productId, Vector = vector };
}