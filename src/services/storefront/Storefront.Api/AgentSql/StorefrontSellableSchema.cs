namespace Storefront.Api.AgentSql;

// R8: izinli vitrin yüzeyinin TEK KAYNAĞI. Bu listeden (a) bootstrap DDL üretilir,
// (b) ChatAgent prompt şema bloğu ELLE yazılır, (c) scripts/check-agent-query-schema.sh
// her kolon adının ConstValues.cs'te geçtiğini doğrular (drift guard).
// DİKKAT: kolon satırı formatı script tarafından grep'lenir — `new("kolon_adı", ...` düzenini koru.
public static class StorefrontSellableSchema
{
    public const string ViewName = "storefront_sellable";

    public record Column(string Name, string PgType, string SourceExpression, string Description);

    // Kaynak jsonb ifadeleri: v = mt_doc_storefrontview, e = mt_doc_productdescriptionembedding.
    // Newtonsoft PascalCase alan adları (yanlış ad SESSİZCE null üretir — 067 canlı bulgu).
    public static readonly IReadOnlyList<Column> Columns =
    [
        new("product_id", "uuid", "v.id", "Ürün kimliği (sepet/benzerlik/kendisi-hariç)"),
        new("name", "text", "v.data->>'Name'", "Kitap adı"),
        new("description", "text", "v.data->>'Description'", "Açıklama metni"),
        new("authors", "text[]",
            "(select coalesce(array_agg(a->>'Name'), '{}') from jsonb_array_elements(v.data->'Authors') a)",
            "Yazar adları (yalnız ad; unnest/ILIKE kalıbı)"),
        new("publisher", "text", "v.data->>'Publisher'", "Yayınevi adı (id'siz)"),
        new("category", "text", "v.data->>'Category'", "Kategori adı (düz ad, ağaç yok)"),
        new("price", "numeric", "(v.data->>'Price')::numeric", "Fiyat (TL)"),
        new("stock", "integer", "(v.data->>'StockQuantity')::integer", "Stok adedi (NULL = stok bilgisi akmadı)"),
        new("rating_average", "numeric", "(v.data->>'RatingAverage')::numeric", "Puan ortalaması (NULL = hiç puan yok)"),
        new("rating_count", "integer", "coalesce((v.data->>'RatingCount')::integer, 0)", "Puan sayısı"),
        new("specs", "jsonb", "v.data->'Specs'",
            "Özellik çiftleri [{Attribute,Option}] (jsonb_array_elements kalıbı)"),
        new("family_code", "text", "v.data->>'FamilyCode'", "Varyant ailesi kodu (NULL = ailesiz)"),
        new("image_url", "text", "v.data->>'ImageUrl'", "Kapak görseli URL"),
        new("added_at", "timestamptz", "v.mt_last_modified", "YAKLAŞIK ekleniş (kayıt güncellenme zamanı)"),
        // indirim (Discount push). view-guard: yalnız now pencere içindeyse indirim görünür — fire
        // gecikse/kalksa liste fiyatına döner (yapısal yedek). Discount fiyat tutmaz → effective_price
        // liste fiyatından hesaplanır (liste değişince otomatik doğru).
        new("discount_pct", "integer",
            "case when (v.data->>'DiscountPct') is not null " +
            "and now() >= (v.data->>'DiscountStartsAt')::timestamptz " +
            "and ((v.data->>'DiscountEndsAt') is null or now() < (v.data->>'DiscountEndsAt')::timestamptz) " +
            "then (v.data->>'DiscountPct')::integer end",
            "Aktif indirim yüzdesi (NULL = indirim yok / pencere dışı)"),
        new("discount_ends_at", "timestamptz",
            "case when (v.data->>'DiscountPct') is not null " +
            "and now() >= (v.data->>'DiscountStartsAt')::timestamptz " +
            "and ((v.data->>'DiscountEndsAt') is null or now() < (v.data->>'DiscountEndsAt')::timestamptz) " +
            "then (v.data->>'DiscountEndsAt')::timestamptz end",
            "Aktif indirim bitişi (NULL = indirim yok / süresiz)"),
        new("effective_price", "numeric",
            "case when (v.data->>'DiscountPct') is not null " +
            "and now() >= (v.data->>'DiscountStartsAt')::timestamptz " +
            "and ((v.data->>'DiscountEndsAt') is null or now() < (v.data->>'DiscountEndsAt')::timestamptz) " +
            "then round((v.data->>'Price')::numeric * (1 - (v.data->>'DiscountPct')::numeric / 100), 2) " +
            "else (v.data->>'Price')::numeric end",
            "Ödenecek etkin fiyat (indirim aktifse indirimli, değilse liste fiyatı)"),
        // vector(1536) ŞART (::vector değil): HNSW ifade-indeksi tiplendirilmiş ifadeyle eşleşir;
        // tipsiz cast'te 20k jsonb detoast + parse her sorguda tekrar eder (canlı ölçüm: 9sn → 35ms).
        new("embedding", "vector(1536)", "(e.data->>'Vector')::vector(1536)",
            "Anlamsal temsil (NULL olabilir; yanıttan ayıklanır)")
    ];

    // Anlamsal sorgu indeksi: mt_doc tablosuna HNSW İFADE indeksi — view'ın embedding ifadesiyle
    // birebir aynı ifade (planner ancak o zaman kullanır). IF NOT EXISTS idempotent; ilk kurulum
    // ~1-2 dk sürer (20k satır), sonraki açılışlar no-op.
    public static string BuildEmbeddingIndexDdl(string schema) =>
        $"create index if not exists idx_agent_embedding_hnsw " +
        $"on {schema}.mt_doc_productdescriptionembedding " +
        $"using hnsw (((data->>'Vector')::vector(1536)) vector_cosine_ops)";

    // DROP+CREATE (CREATE OR REPLACE kolon değişikliğinde kırılır); grant'lar bootstrap'ta yeniden verilir.
    // Satılabilirlik filtresi GÖMÜLÜ: yayından kalkan/eksik satır yüzeyde HİÇ yok (FR-005/SC-004 yapısal).
    public static string BuildViewDdl(string schema)
    {
        var selectList = string.Join(",\n    ",
            Columns.Select(c => $"{c.SourceExpression} as {c.Name}"));

        return $"""
            drop view if exists {schema}.{ViewName};
            create view {schema}.{ViewName} as
            select
                {selectList}
            from {schema}.mt_doc_storefrontview v
            left join {schema}.mt_doc_productdescriptionembedding e on e.id = v.id
            where (v.data->>'IsDeleted')::boolean is not true
              and v.data->>'Name' is not null
              and v.data->>'Price' is not null;
            """;
    }
}
