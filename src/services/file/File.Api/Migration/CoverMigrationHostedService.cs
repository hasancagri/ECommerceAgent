using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FileApi.Migration;

// Bir-kez kapak besleme (config-gated, idempotent). Açılışta seed JSON kaynağını okur (091; eski xlsx
// emekli); her satır: skip-existing → indir (timeout) → IFileStore.Put. Per-satır try/catch (bozuk satır
// durdurmaz). Özet {yazıldı, atlandı, başarısız} log'lanır. Enabled=false → erken döner.
public sealed class CoverMigrationHostedService : BackgroundService
{
    private const string DefaultContentType = "application/octet-stream";

    private readonly CoverMigrationOptions _options;
    private readonly IFileStore _store;
    private readonly JsonCoverSource _source;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<CoverMigrationHostedService> _log;

    public CoverMigrationHostedService(
        CoverMigrationOptions options,
        IFileStore store,
        JsonCoverSource source,
        IHttpClientFactory httpFactory,
        ILogger<CoverMigrationHostedService> log)
    {
        _options = options;
        _store = store;
        _source = source;
        _httpFactory = httpFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(_options.SourceJsonPath) || !System.IO.File.Exists(_options.SourceJsonPath))
        {
            _log.LogWarning("Cover migration atlandı: kaynak seed JSON bulunamadı ({Path}).", _options.SourceJsonPath);
            return;
        }

        var timeoutSeconds = _options.DownloadTimeoutSeconds > 0 ? _options.DownloadTimeoutSeconds : 30;
        var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        if (!string.IsNullOrWhiteSpace(_options.UserAgent))
            http.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);

        var concurrency = _options.DownloadConcurrency > 0 ? _options.DownloadConcurrency : 2;
        var retryCount = _options.RetryCount > 0 ? _options.RetryCount : 1;
        var minDelayMs = _options.MinDelayMs > 0 ? _options.MinDelayMs : 0;
        int written = 0, skipped = 0, failed = 0, processed = 0;

        var rows = _source.Read(_options.SourceJsonPath);
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = concurrency,
            CancellationToken = stoppingToken
        };

        _log.LogInformation(
            "Cover migration başladı (eşzamanlılık={Concurrency} retry={Retry} gecikme={Delay}ms).",
            concurrency, retryCount, minDelayMs);

        try
        {
            await Parallel.ForEachAsync(rows, parallelOptions, async (row, ct) =>
            {
                var (isbn, imageUrl) = row;
                try
                {
                    if (!CoverKey.TryCreate(isbn, out var key)) { Interlocked.Increment(ref failed); return; }

                    var exists = await _store.ExistsAsync(key, ct);
                    if (MigrationDecision.Decide(exists, imageUrl) == CoverMigrationDecision.Skip)
                    {
                        Interlocked.Increment(ref skipped);
                        return;
                    }

                    // İndir (retry/backoff) → tam byte tampon → sonra yaz. Yarım/bozuk dosya diske düşmez.
                    var download = await DownloadWithRetryAsync(http, imageUrl!, retryCount, isbn, ct);
                    if (download is null) { Interlocked.Increment(ref failed); return; }

                    await using (var ms = new MemoryStream(download.Value.Bytes))
                    {
                        await _store.PutAsync(key, ms, download.Value.ContentType, ct);
                    }
                    Interlocked.Increment(ref written);

                    // Throttle: kaynağı/DNS resolver'ı boğma (yavaş ama temiz).
                    if (minDelayMs > 0) await Task.Delay(minDelayMs, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    _log.LogWarning(ex, "Kapak yazılamadı (isbn={Isbn}).", isbn);
                }
                finally
                {
                    var n = Interlocked.Increment(ref processed);
                    if (n % 500 == 0)
                        _log.LogInformation(
                            "Cover migration ilerleme: işlenen={Processed} yazıldı={Written} atlandı={Skipped} başarısız={Failed}.",
                            n, Volatile.Read(ref written), Volatile.Read(ref skipped), Volatile.Read(ref failed));
                }
            });
        }
        catch (OperationCanceledException)
        {
            _log.LogWarning("Cover migration iptal edildi (kapatma).");
        }

        _log.LogInformation(
            "Cover migration bitti: yazıldı={Written} atlandı={Skipped} başarısız={Failed}.",
            written, skipped, failed);
    }

    // Görseli retry/backoff ile indirir. 404/kalıcı hata → null (retry yok). 429/5xx/transient → yeniden.
    private async Task<(byte[] Bytes, string ContentType)?> DownloadWithRetryAsync(
        HttpClient http, string imageUrl, int retryCount, string isbn, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= retryCount; attempt++)
        {
            try
            {
                using var resp = await http.GetAsync(imageUrl, ct);

                // Kalıcı istemci hatası (404 vb.) → boşuna deneme.
                if (resp.StatusCode is System.Net.HttpStatusCode.NotFound
                    or System.Net.HttpStatusCode.Gone
                    or System.Net.HttpStatusCode.BadRequest)
                    return null;

                // Geçici (429/5xx) → backoff'la yeniden.
                var status = (int)resp.StatusCode;
                if (status == 429 || status >= 500)
                {
                    if (attempt == retryCount) return null;
                    await BackoffAsync(attempt, ct);
                    continue;
                }

                if (!resp.IsSuccessStatusCode) return null;

                var contentType = resp.Content.Headers.ContentType?.ToString() ?? DefaultContentType;
                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                if (bytes.Length == 0) return null;
                return (bytes, contentType);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Transient (DNS/socket/timeout) → backoff'la yeniden; son denemede pes et.
                if (attempt == retryCount)
                {
                    _log.LogWarning(ex, "Kapak indirilemedi, denemeler bitti (isbn={Isbn}).", isbn);
                    return null;
                }
                await BackoffAsync(attempt, ct);
            }
        }
        return null;
    }

    // Lineer backoff: 1s, 2s, 3s ... (deneme sayısı × 1s). Kaynağı boğmadan kurtarma penceresi.
    private static Task BackoffAsync(int attempt, CancellationToken ct)
        => Task.Delay(TimeSpan.FromSeconds(attempt), ct);
}
