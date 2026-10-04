
namespace Storefront.Api.Projections;

// 086 R1: event-log → Elasticsearch async projeksiyonu. Marten daemon her batch'te etkilenen
// ProductId'leri `AggregateStreamAsync<StorefrontDocument>` ile TAZE katlar (saf fold, İLKE VI) →
// satılabilirse ES'e yazar (upsert), değilse siler (FR-008 projeksiyon-zamanı dışlama).
// Embedding: açıklama değişince üretilir, aksi halde ES'teki önceki doc'un vektörü korunur (R6).
// Rebuild = ES index sil+kur + daemon RebuildProjectionAsync (US3). Daemon offset Postgres'te → idempotent.
public class StorefrontProjection(
    ElasticsearchClient client,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator) : IProjection
{
    public async Task ApplyAsync(
        IDocumentOperations operations,
        IReadOnlyList<IEvent> events,
        CancellationToken cancellation)
    {
        var now = DateTimeOffset.UtcNow;
        var index = StorefrontSearchIndex.IndexName;

        foreach (var streamId in events.Select(e => e.StreamId).Distinct())
        {
            var doc = await operations.Events.AggregateStreamAsync<StorefrontDocument>(streamId, token: cancellation);

            // null stream (silinmiş) ya da satılamaz (IsDeleted / Price yok) → ES'ten sil (yoksa no-op).
            if (doc is null || !doc.IsSellable)
            {
                var del = await client.Transport.RequestAsync<StringResponse>(
                    new EndpointPath(Elastic.Transport.HttpMethod.DELETE, $"/{index}/_doc/{streamId:D}"),
                    null, null, null, cancellationToken: cancellation);
                // 404 = zaten yok (satılamaz kitap index'e hiç girmemiş) → başarı sayılır.
                if (del.ApiCallDetails.HttpStatusCode is not (200 or 404))
                    throw new InvalidOperationException(
                        $"ES sil başarısız ({streamId}): {del.ApiCallDetails.DebugInformation}");
                continue;
            }

            // Önceki ES doc'u oku (embedding kararı + added_at korunumu için) — yalnız gerekli alanlar.
            string? oldDescription = null;
            float[]? oldEmbedding = null;
            DateTimeOffset addedAt = now;
            var get = await client.Transport.RequestAsync<StringResponse>(
                new EndpointPath(Elastic.Transport.HttpMethod.GET,
                    $"/{index}/_doc/{streamId:D}?_source_includes=description,embedding,added_at"),
                null, null, null, cancellationToken: cancellation);
            if (get.ApiCallDetails.HttpStatusCode == 200)
            {
                using var existing = JsonDocument.Parse(get.Body);
                if (existing.RootElement.TryGetProperty("_source", out var src))
                {
                    if (src.TryGetProperty("description", out var d))
                        oldDescription = d.GetString();
                    if (src.TryGetProperty("embedding", out var e) && e.ValueKind == JsonValueKind.Array)
                        oldEmbedding = e.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray();
                    if (src.TryGetProperty("added_at", out var a)
                        && a.ValueKind == JsonValueKind.String && a.TryGetDateTimeOffset(out var parsed))
                        addedAt = parsed;
                }
            }

            // Embedding kararı (R6): açıklama değişti/eksik → üret; aynı+var → koru; boş → yok.
            var embedding = StorefrontDocument.DecideEmbedding(doc.Description, oldDescription, oldEmbedding is not null) switch
            {
                EmbeddingDecision.Keep => oldEmbedding,
                EmbeddingDecision.Clear => null,
                EmbeddingDecision.Generate => (await embeddingGenerator.GenerateVectorAsync(
                    doc.Description!, cancellationToken: cancellation)).ToArray(),
                _ => null
            };

            // ES doc gövdesi — null=alan yok sözleşmesi: yalnız bilinen/aktif alanları yaz.
            var body = new Dictionary<string, object?>
            {
                ["product_id"] = doc.Id.ToString("D"),
                ["name"] = doc.Name,
                ["description"] = doc.Description,
                ["authors"] = doc.Authors,
                ["publisher"] = doc.Publisher,
                ["category"] = doc.Category,
                ["price"] = doc.Price,
                ["effective_price"] = doc.EffectivePrice(now),
                ["image_url"] = doc.ImageUrl,
                ["family_code"] = doc.FamilyCode,
                ["added_at"] = addedAt,
            };
            if (doc.Stock is not null) body["stock"] = doc.Stock;
            if (doc.RatingCount > 0)
            {
                body["rating_average"] = doc.RatingAverage;
                body["rating_count"] = doc.RatingCount;
            }
            if (doc.Specs.Count > 0)
                body["specs"] = doc.Specs.Select(s => new { attribute = s.Attribute, option = s.Option }).ToList();
            if (doc.IsDiscountActive(now))
            {
                body["discount_pct"] = doc.DiscountPct;
                body["discount_ends_at"] = doc.DiscountEndsAt;
            }
            if (embedding is not null)
                body["embedding"] = embedding;

            var put = await client.Transport.RequestAsync<StringResponse>(
                new EndpointPath(Elastic.Transport.HttpMethod.PUT, $"/{index}/_doc/{doc.Id:D}"),
                PostData.String(System.Text.Json.JsonSerializer.Serialize(body)), null, null, cancellationToken: cancellation);
            if (!put.ApiCallDetails.HasSuccessfulStatusCode)
                throw new InvalidOperationException(
                    $"ES index yazımı başarısız ({doc.Id}): {put.ApiCallDetails.DebugInformation}");
        }
    }
}