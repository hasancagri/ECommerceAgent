namespace Storefront.Api;

// geçmiş katalog için anlamsal temsil doldurma (FR-008/SC-004). Her açılışta idempotent tarama:
// açıklaması dolu ama temsil dokümanı OLMAYAN satırlar batch'ler halinde embed edilir; iş yoksa no-op.
// Docker reset + reseed sonrası elle tetiksiz kendiliğinden iyileşir. Endpoint/scope yüzeyi bilinçli YOK.
public class EmbeddingBackfillService(
    IDocumentStore store,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    EmbeddingBackfillOption options,
    ILogger<EmbeddingBackfillService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Hata host'u ÇÖKERTMEMELİ (BackgroundService exception default'u StopHost'tur) — logla ve çık;
        // sonraki açılış kaldığı yerden tarar. stoppingToken iptali normal kapanıştır, hata değil.
        try
        {
            var totalEmbedded = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var session = store.LightweightSession();

                // Temsili zaten olan id'ler (yalnız PK projeksiyonu — doküman gövdesi çekilmez).
                var embeddedIds = await session.Query<Domains.StorefrontView.ProductDescriptionEmbedding>()
                    .Select(x => x.ProductId)
                    .ToListAsync(stoppingToken);

                // Aday: açıklaması dolu, temsili olmayan satır. IsDeleted filtrelenmez — temsil yayın
                // durumundan bağımsız saklanır (yayına dönüşte yeniden üretim gerekmez, research R4).
                var batch = await session.Query<StorefrontView>()
                    .Where(x => x.Description != null && x.Description != string.Empty
                                && !embeddedIds.Contains(x.ProductId))
                    .OrderBy(x => x.ProductId)
                    .Take(options.BackfillBatchSize)
                    .ToListAsync(stoppingToken);

                if (batch.Count == 0)
                    break;

                var embeddings = await embeddingGenerator.GenerateAsync(
                    batch.Select(x => x.Description!), cancellationToken: stoppingToken);

                for (var i = 0; i < batch.Count; i++)
                    session.Store(Domains.StorefrontView.ProductDescriptionEmbedding.Create(
                        batch[i].ProductId, embeddings[i].Vector.ToArray()));

                await session.SaveChangesAsync(stoppingToken);
                totalEmbedded += batch.Count;
                logger.LogInformation("Embedding backfill: {Count} satır işlendi (toplam {Total})",
                    batch.Count, totalEmbedded);
            }

            logger.LogInformation("Embedding backfill tamamlandı: {Total} satır dolduruldu (0 = iş yoktu)",
                totalEmbedded);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // normal kapanış
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Embedding backfill başarısız — servis açık kalır, sonraki açılışta tekrar denenir");
        }
    }
}