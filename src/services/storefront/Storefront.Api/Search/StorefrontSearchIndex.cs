
namespace Storefront.Api.Search;

// 086 R4: arama index'inin TEK kaynağı — sabit ad + mapping (sunucu-sahipli; LLM index/mapping vermez).
// Mapping sözleşmesi contracts/search-index-mapping.json ile birebir: turkish analyzer + dense_vector
// 1536/cosine + nested specs. _id = ProductId (R5). embedding dönüşte _source whitelist'iyle ayıklanır
// (yazılır ama sorgu yanıtına girmez).
public static class StorefrontSearchIndex
{
    public const string IndexName = "storefront-books";

    // _source whitelist (embedding HARİÇ) — sorgu yanıtına dönen sabit alan kümesi (rail). product_id
    // KALIR: agent anahtarı (sepete-ekle / benzer / yorum zinciri); kullanıcıya ham gösterme kuralı playbook'ta.
    public static readonly string[] SourceFields =
    [
        "product_id", "name", "description", "authors", "publisher", "category",
        "price", "effective_price", "stock", "rating_average", "rating_count",
        "specs", "family_code", "image_url", "added_at", "discount_pct", "discount_ends_at"
    ];

    // contracts/search-index-mapping.json gövdesi (settings + mappings). Kolon/analyzer driftinde
    // sözleşme dosyası + bu sabit birlikte güncellenir.
    public const string MappingJson = """
        {
          "settings": {
            "analysis": {
              "analyzer": {
                "tr_text": { "type": "turkish" }
              }
            }
          },
          "mappings": {
            "properties": {
              "product_id": { "type": "keyword" },
              "name": {
                "type": "text",
                "analyzer": "tr_text",
                "fields": { "keyword": { "type": "keyword", "ignore_above": 512 } }
              },
              "description": { "type": "text", "analyzer": "tr_text" },
              "authors": { "type": "text", "analyzer": "tr_text" },
              "publisher": {
                "type": "text",
                "analyzer": "tr_text",
                "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } }
              },
              "category": {
                "type": "text",
                "analyzer": "tr_text",
                "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } }
              },
              "price": { "type": "float" },
              "effective_price": { "type": "float" },
              "stock": { "type": "integer" },
              "rating_average": { "type": "float" },
              "rating_count": { "type": "integer" },
              "specs": {
                "type": "nested",
                "properties": {
                  "attribute": { "type": "keyword" },
                  "option": { "type": "keyword" }
                }
              },
              "family_code": { "type": "keyword" },
              "image_url": { "type": "keyword", "index": false },
              "added_at": { "type": "date" },
              "discount_pct": { "type": "integer" },
              "discount_ends_at": { "type": "date" },
              "embedding": {
                "type": "dense_vector",
                "dims": 1536,
                "index": true,
                "similarity": "cosine"
              }
            }
          }
        }
        """;

    // Index yoksa mapping'le kurar; varsa no-op (idempotent açılış).
    public static async Task EnsureAsync(ElasticsearchClient client, CancellationToken ct)
    {
        var head = await client.Transport.RequestAsync<StringResponse>(
            new EndpointPath(Elastic.Transport.HttpMethod.HEAD, $"/{IndexName}"), null, null, null, cancellationToken: ct);

        if (head.ApiCallDetails.HttpStatusCode is 200 or 204)
            return;

        await CreateAsync(client, ct);
    }

    private static async Task CreateAsync(ElasticsearchClient client, CancellationToken ct)
    {
        var create = await client.Transport.RequestAsync<StringResponse>(
            new EndpointPath(Elastic.Transport.HttpMethod.PUT, $"/{IndexName}"),
            PostData.String(MappingJson), null, null, cancellationToken: ct);

        if (!create.ApiCallDetails.HasSuccessfulStatusCode)
            throw new InvalidOperationException(
                $"Elasticsearch index kurulamadı ({IndexName}): {create.ApiCallDetails.DebugInformation}");
    }
}

// Açılışta index'i garanti eden hosted service. StartAsync bitmeden app trafiğe çıkmaz (eski
// AgentQuerySurfaceBootstrap emsali) — ilk sorgu index hazırken gelir. ES hazır değilse birkaç kez dener
// (Aspire WaitFor çoğunu kapatır; kısa boot yarışı için yedek).
public class StorefrontSearchIndexBootstrap(
    ElasticsearchClient client,
    ILogger<StorefrontSearchIndexBootstrap> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await StorefrontSearchIndex.EnsureAsync(client, cancellationToken);
                logger.LogInformation("Elasticsearch arama index'i hazır: {Index}", StorefrontSearchIndex.IndexName);
                return;
            }
            catch (Exception ex) when (attempt < 10 && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "ES index kurulumu {Attempt}. denemede başarısız — yeniden denenecek", attempt);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}