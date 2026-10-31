using Catalog.Api.Embeddings;
using Catalog.Api.Options;

namespace Catalog.Api.Process;

// 096: BC-içi dayanıklı embedding reconcile döngüsü (Process/ — kullanıcı tetiklemez, BC'nin KENDİ
// süreci). Her turda vektörü eksik/bayat (içerik-hash ∨ model farkı) kitapları KENDİ DB'sinden bulur,
// Ollama'yla toplu vektörler, upsert eder; aday bitince IntervalSeconds uyur. Broker yok, seed'den
// bağımsız (eventual). Fail-soft (FR-005): Ollama erişilemez/bozuk yanıt → batch loglanıp atlanır,
// sonraki tur dener; host ÇÖKMEZ. Kaçan hiçbir sinyal kalıcı bayatlık bırakmaz — sonraki tarama kapatır.
public class EmbeddingReconcileProcess(
    IServiceProvider services,
    EmbeddingOptions options,
    ILogger<EmbeddingReconcileProcess> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Embedding reconcile kapalı (EmbeddingOptions.Enabled=false) — süreç koşmayacak.");
            return;
        }

        logger.LogInformation("Embedding reconcile başladı: model={Model}, batch={BatchSize}, aralık={Interval}s.",
            options.Model, options.BatchSize, options.IntervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.IntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            // TUZAK: HttpClient timeout'u da TaskCanceledException üretir — host kapanışıyla KARIŞTIRMA;
            // ayrım stoppingToken (memory: httpclient-timeout-taskcanceled-stophost).
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                logger.LogWarning("Embedding modeli Ollama'da yok (404): 'ollama pull {Model}' ile çekin. " +
                    "Sonraki döngüde yeniden denenecek.", options.Model);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning("Embedding üretimi erişim hatası (Ollama kapalı/ulaşılamaz olabilir): {Message}. " +
                    "Sonraki döngüde yeniden denenecek.", ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Embedding reconcile turu hata verdi; sonraki döngüde yeniden denenecek.");
            }
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Embedding reconcile durdu.");
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var client = scope.ServiceProvider.GetRequiredService<OllamaEmbeddingClient>();

        // Aday seçimi: ürün + yazar + kategori yüklenir, metin compose+hash'lenir, mevcut kayıtla
        // (yalnız hash/model projeksiyonu — vektör kolonu çekilmez) karşılaştırılır.
        var categories = (await session.Query<Category>().ToListAsync(ct)).ToDictionary(c => c.Id);
        var authorNames = (await session.Query<Author>().ToListAsync(ct)).ToDictionary(a => a.Id, a => a.Name);
        var existing = (await session.Query<ProductEmbedding>()
                .Select(e => new { e.Id, e.ContentHash, e.Model })
                .ToListAsync(ct))
            .ToDictionary(e => e.Id);

        var products = await session.Query<Product>().ToListAsync(ct);

        var candidates = new List<(Guid ProductId, string Text, string Hash)>();
        foreach (var product in products)
        {
            var names = product.AuthorIds
                .Select(id => authorNames.GetValueOrDefault(id))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .ToList();
            var (_, chainNames) = CategoryNaming.ResolveChain(product.CategoryId, categories);

            var text = EmbeddingText.Compose(product.Name, product.Description, names, chainNames, options.MaxChars);
            var hash = EmbeddingText.Hash(text);

            existing.TryGetValue(product.Id, out var record);
            if (ProductEmbedding.NeedsRefresh(record?.ContentHash, record?.Model, hash, options.Model))
                candidates.Add((product.Id, text, hash));
        }

        if (candidates.Count == 0)
        {
            logger.LogDebug("Embedding reconcile: aday yok ({Total} ürün güncel).", products.Count);
            return;
        }

        logger.LogInformation("Embedding reconcile: {Candidates}/{Total} ürün vektörlenecek (model={Model}).",
            candidates.Count, products.Count, options.Model);

        // Batch başına embed + upsert + commit: yarıda kesilme kaldığı yerden sürer (FR-002).
        var produced = 0;
        foreach (var batch in candidates.Chunk(options.BatchSize))
        {
            ct.ThrowIfCancellationRequested();

            var vectors = await client.EmbedAsync(batch.Select(c => c.Text).ToList(), ct);
            for (var i = 0; i < batch.Length; i++)
                session.Store(ProductEmbedding.Create(batch[i].ProductId, vectors[i], batch[i].Hash, options.Model));
            await session.SaveChangesAsync(ct);

            produced += batch.Length;
            logger.LogInformation("Embedding reconcile ilerleme: {Produced}/{Candidates}.", produced, candidates.Count);
        }

        logger.LogInformation("Embedding reconcile turu bitti: {Produced} vektör üretildi.", produced);
    }
}
