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
    public const string EmbeddingTable = $"{Schema}.product_embedding";
    public const string Bm25Index = "product_bm25_idx";

    // Alan tokenizasyonu: ICU (Unicode/locale-duyarlı; built-in Türkçe config yok) + lowercase.
    // FLAG (İ/ı): ICU'nun ascii_folding arg'ı sürüme göre değişir → sorgu tarafı ayrıca foldlanır
    // (SearchTextNormalization). ascii_folding gerekirse burada '::pdb.icu' yerine ilgili arg eklenir.
    private const string Icu = "::pdb.icu";

    // BM25 index — mt_doc_product JSONB üstünde (yeni tablo YOK; MVCC otomatik bakım). Ad+açıklama+etiket.
    // Tags = BookTag[] JSONB → text olarak indekslenir (değer kelimeleri aranır; 'Value' gürültüsü kabul).
    private static readonly string CreateBm25 = $"""
        CREATE INDEX IF NOT EXISTS {Bm25Index}
        ON {ProductTable}
        USING bm25 (
            id,
            ((data->>'Name'){Icu}),
            ((data->>'Description'){Icu}),
            ((data->>'Tags'){Icu})
        );
        """;

    private static readonly string[] Statements =
    [
        "CREATE EXTENSION IF NOT EXISTS pg_search;",
        "CREATE EXTENSION IF NOT EXISTS vector;",
        CreateBm25,
        $"""
         CREATE TABLE IF NOT EXISTS {EmbeddingTable} (
             product_id uuid PRIMARY KEY,
             embedding  vector(1536) NOT NULL
         );
         """,
        $"""
         CREATE INDEX IF NOT EXISTS product_embedding_hnsw
         ON {EmbeddingTable} USING hnsw (embedding vector_cosine_ops);
         """,
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
