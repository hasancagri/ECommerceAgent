using System.Globalization;
using System.Text;
using Catalog.Api.Search;
using Marten;
using Microsoft.Extensions.AI;

namespace Catalog.Api.Process;

// 025 Dilim-1 (Process/ — BC'nin KENDİ dayanıklı işi, mesaj/kuyruk YOK; ImportJobWorker deseni):
// embedding'i OLMAYAN ürünleri yoklar (LEFT JOIN product_embedding IS NULL) → batch embed → upsert.
// Backfill (mevcut 22k) + artımlı (yeni ProductAdded) TEK mekanizma: yeni ürün embedding'siz doğar,
// sonraki tick yakalar — ayrı ProductAdded consumer GEREKMEZ. Resumable: durum izlemez, her tick
// "eksik olan"ı bulur; crash'te kaldığı yerden sürer. Hepsi embed → boşta bekler (idempotent: ikinci
// açılışta eksik yok → OpenAI çağrısı yok). Kayıt: Program.cs AddHostedService (Wolverine keşfiyle İLGİSİZ).
public sealed class EmbeddingBackfillWorker(
    IDocumentStore store,
    NpgsqlDataSource dataSource,
    EmbeddingClientFactory clientFactory,
    CatalogEmbeddingOptions options,
    ILogger<EmbeddingBackfillWorker> logger) : BackgroundService
{
    private const string Schema = SchemaConstants.CatalogSchemaName;
    private const string ProductTable = $"{Schema}.mt_doc_product";
    private const string EmbeddingTable = $"{Schema}.product_embedding";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var poll = TimeSpan.FromSeconds(options.PollSeconds <= 0 ? 10 : options.PollSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            var embedded = 0;
            try
            {
                embedded = await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // OpenAI/DB hatası işi öldürmez — eksik ürünler kalır, sonraki tick yeniden dener.
                logger.LogError(ex, "Embedding backfill turu başarısız");
            }

            // İş vardıysa hemen devam et (hızlı boşalt); yoksa poll aralığı bekle.
            if (embedded > 0)
                continue;
            try
            {
                await Task.Delay(poll, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // Bir tur: eksik id'leri bul → ürünleri yükle → batch embed → upsert. Dönüş = embed edilen adet.
    private async Task<int> TickAsync(CancellationToken ct)
    {
        var batch = options.BackfillBatchSize <= 0 ? 100 : options.BackfillBatchSize;
        var ids = await FindMissingAsync(batch, ct);
        if (ids.Count == 0)
            return 0;

        await using var session = store.LightweightSession();
        var products = await session.Query<Product>().Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        if (products.Count == 0)
            return 0;

        var inputs = products
            .Select(p => EmbeddingText.Build(p.Name, p.Description, p.Genre, p.Moods, p.Themes))
            .ToList();

        var embeddings = await clientFactory.Generator.GenerateAsync(inputs, cancellationToken: ct);

        for (var i = 0; i < products.Count; i++)
            await UpsertAsync(products[i].Id, embeddings[i].Vector, EmbeddingText.Hash(inputs[i]), ct);

        logger.LogInformation("Embedding backfill: {Count} ürün embed edildi", products.Count);
        return products.Count;
    }

    // Embedding satırı olmayan ürün id'leri (LEFT JOIN … IS NULL). Sıralı → deterministik ilerleme.
    private async Task<List<Guid>> FindMissingAsync(int limit, CancellationToken ct)
    {
        var sql =
            $"SELECT p.id FROM {ProductTable} p " +
            $"LEFT JOIN {EmbeddingTable} e ON e.product_id = p.id " +
            $"WHERE e.product_id IS NULL ORDER BY p.id LIMIT {limit}";

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        var ids = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetGuid(0));
        return ids;
    }

    // Upsert: vektör text literal `[...]` + `::vector` CAST (Npgsql vector plugin gerekmez). input_hash
    // ileride değişim-algılama için saklanır (v1 worker yalnız eksik olanı yazar → hep INSERT yolu).
    private async Task UpsertAsync(Guid productId, ReadOnlyMemory<float> vector, string hash, CancellationToken ct)
    {
        var sql =
            $"INSERT INTO {EmbeddingTable} (product_id, embedding, input_hash) " +
            "VALUES ($1, $2::vector, $3) " +
            "ON CONFLICT (product_id) DO UPDATE SET " +
            "embedding = EXCLUDED.embedding, input_hash = EXCLUDED.input_hash, updated_at = now()";

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = productId });
        cmd.Parameters.Add(new NpgsqlParameter { Value = ToVectorLiteral(vector.Span) });
        cmd.Parameters.Add(new NpgsqlParameter { Value = hash });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string ToVectorLiteral(ReadOnlySpan<float> vector)
    {
        var sb = new StringBuilder(vector.Length * 8).Append('[');
        for (var i = 0; i < vector.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(vector[i].ToString(CultureInfo.InvariantCulture));
        }
        return sb.Append(']').ToString();
    }
}
