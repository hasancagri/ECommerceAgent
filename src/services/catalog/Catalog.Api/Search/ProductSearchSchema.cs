namespace Catalog.Api.Search;

// 011 T005/T010: hibrit arama şema nesneleri (ParadeDB) — Marten'in LINQ/şema akışı DIŞINDA, ham DDL.
// Marten mt_doc_product'u ApplyAllDatabaseChangesOnStartup'ta kurar; bu hosted service ONDAN SONRA
// koşar (Program.cs'te AddCatalogMarten'den SONRA kaydedilir → hosted-service sırası deterministik).
// Tüm DDL idempotent (IF NOT EXISTS); her ifade bağımsız (biri patlarsa ötekiler kurulur — US1 BM25
// ayakta kalır US2 embedding DDL'i sürüm-uyumsuzsa). FLAG'ler canlı doğrulanır (quickstart).
public sealed class ProductSearchSchemaInitializer(
    NpgsqlDataSource dataSource,
    ILogger<ProductSearchSchemaInitializer> logger) : IHostedService
{
    private const string Schema = SchemaConstants.CatalogSchemaName; // catalogManagement
    private const string ProductTable = $"{Schema}.mt_doc_product";
    private const string EmbeddingTable = $"{Schema}.product_embedding"; // 025: embedding omurgası (revive)
    private const string Bm25Index = "product_bm25_idx";

    // BM25 index — mt_doc_product'ın TÜM `data` JSONB'si, json_fields fast+expand_dots ile (018, ParadeDB
    // 0.26.6). `fast` → numeric alt-alanlar range + fast; `expand_dots` → nested yollar (Price.Amount).
    // Böylece tek index TÜM sorguyu karşılar (lexical + yapısal filtre + sort + kategori). Sorgu deseni:
    // `WHERE id @@@ 'data.Name:x AND data.Price.Amount:[100 TO 300] AND data.CategoryId:"<uuid>"'`
    // (alan yolu `data.<path>`, anchor `id @@@`); relevance Top-K `ORDER BY paradedb.score(id)`.
    // Türkçe (İ/ı): default tokenizer bugünkü ILIKE ile PARİTE (yaygın ASCII katlar; kusursuz İ/ı folding
    // backlog — regresyon DEĞİL, 0.26.6 tokenizer'ları da Türkçe-özel katlamıyor). Tek ParadeDB index kuralı:
    // bir relation'da tek bm25 index olur. (011 "data defined more than once" kısıtı — tek `data` korunur.)
    private static readonly string CreateBm25 =
        $"CREATE INDEX IF NOT EXISTS {Bm25Index} ON {ProductTable} " +
        "USING bm25 (id, data) WITH (json_fields='{\"data\": {\"fast\": true, \"expand_dots\": true}}');";

    // 025 Dilim-1 embedding omurgası: pgvector eklenti + ayrı tablo (native vector(1536) kolon) + HNSW.
    // Marten `data` jsonb'ye DOKUNMAZ (ayrı tablo; kNN ham-SQL, mevcut BM25 gibi). Vektör yazımı text
    // literal + `::vector` CAST ile (Npgsql vector plugin gerekmez). HNSW opsiyonel/ileri-dönük — 22k'da
    // exact-scan ms-ölçekli; ifade-bağımsızlığı sayesinde sürüm-uyumsuzsa tablo yine kurulur. Boyut 1536
    // = text-embedding-3-small (CatalogEmbeddingOptions.Dimensions ile hizalı; DDL sabit).
    private const string EmbeddingDim = "1536";
    private static readonly string CreateEmbeddingTable =
        $"CREATE TABLE IF NOT EXISTS {EmbeddingTable} (" +
        "product_id uuid PRIMARY KEY, " +
        $"embedding vector({EmbeddingDim}) NOT NULL, " +
        "input_hash text NOT NULL, " +
        "updated_at timestamptz NOT NULL DEFAULT now());";
    private static readonly string CreateEmbeddingHnsw =
        "CREATE INDEX IF NOT EXISTS product_embedding_hnsw " +
        $"ON {EmbeddingTable} USING hnsw (embedding vector_cosine_ops);";

    private static readonly string[] Statements =
    [
        "CREATE EXTENSION IF NOT EXISTS pg_search;",
        CreateBm25,
        "CREATE EXTENSION IF NOT EXISTS vector;",
        CreateEmbeddingTable,
        CreateEmbeddingHnsw,
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        foreach (var sql in Statements)
        {
            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Bir DDL sürüm-uyumsuzsa (ör. BM25 WITH yazımı) iş durmaz — ötekiler kurulur,
                // ilgili arama ayağı canlıda çalışmaz; log operatöre FLAG'i söyler.
                logger.LogError(ex, "011 arama şema DDL'i başarısız (atlandı): {Sql}", sql.Trim());
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
