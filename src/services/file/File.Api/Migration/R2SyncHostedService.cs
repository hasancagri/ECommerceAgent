using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FileApi.Migration;

// Yerel disk kapaklarını (RootPath/covers/{isbn} + {isbn}.ct) IFileStore'a (R2) kopyalar.
// Yeniden İNDİRME YOK — diskteki byte + .ct content-type doğrudan R2'ye yazılır. İdempotent
// (R2'de varsa atla). Config-gated (SyncLocalToR2). openlibrary'ye dokunmaz.
public sealed class R2SyncHostedService : BackgroundService
{
    private const string DefaultContentType = "application/octet-stream";

    private readonly CoverStoreOptions _store;
    private readonly CoverMigrationOptions _migration;
    private readonly IFileStore _target;
    private readonly ILogger<R2SyncHostedService> _log;

    public R2SyncHostedService(
        CoverStoreOptions store,
        CoverMigrationOptions migration,
        IFileStore target,
        ILogger<R2SyncHostedService> log)
    {
        _store = store;
        _migration = migration;
        _target = target;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_migration.SyncLocalToR2 || _store.Backend != CoverStoreBackend.R2)
            return;

        var coversDir = Path.Combine(_store.RootPath, "covers");
        if (!Directory.Exists(coversDir))
        {
            _log.LogWarning("R2 sync atlandı: yerel covers dizini yok ({Dir}).", coversDir);
            return;
        }

        var concurrency = _migration.DownloadConcurrency > 0 ? _migration.DownloadConcurrency : 4;
        int uploaded = 0, skipped = 0, failed = 0, processed = 0;

        // .ct sidecar'ları hariç, her içerik dosyası bir ISBN.
        var files = Directory.EnumerateFiles(coversDir).Where(f => !f.EndsWith(".ct"));
        if (_migration.SyncLimit > 0)
        {
            files = files.Take(_migration.SyncLimit);
            _log.LogInformation("R2 sync SMOKE: yalnız ilk {Limit} dosya.", _migration.SyncLimit);
        }
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = concurrency,
            CancellationToken = stoppingToken
        };

        _log.LogInformation("R2 sync başladı (kaynak={Dir} eşzamanlılık={Concurrency}).", coversDir, concurrency);

        try
        {
            await Parallel.ForEachAsync(files, parallelOptions, async (path, ct) =>
            {
                var isbn = Path.GetFileName(path);
                try
                {
                    if (!CoverKey.TryCreate(isbn, out var key)) { Interlocked.Increment(ref failed); return; }

                    if (await _target.ExistsAsync(key, ct)) { Interlocked.Increment(ref skipped); return; }

                    var ctPath = path + ".ct";
                    var contentType = System.IO.File.Exists(ctPath)
                        ? (await System.IO.File.ReadAllTextAsync(ctPath, ct)).Trim()
                        : DefaultContentType;
                    if (string.IsNullOrWhiteSpace(contentType)) contentType = DefaultContentType;

                    await using var stream = System.IO.File.OpenRead(path);
                    await _target.PutAsync(key, stream, contentType, ct);
                    Interlocked.Increment(ref uploaded);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    _log.LogWarning(ex, "R2'ye yüklenemedi (isbn={Isbn}).", isbn);
                }
                finally
                {
                    var n = Interlocked.Increment(ref processed);
                    if (n % 500 == 0)
                        _log.LogInformation(
                            "R2 sync ilerleme: işlenen={Processed} yüklendi={Uploaded} atlandı={Skipped} başarısız={Failed}.",
                            n, Volatile.Read(ref uploaded), Volatile.Read(ref skipped), Volatile.Read(ref failed));
                }
            });
        }
        catch (OperationCanceledException)
        {
            _log.LogWarning("R2 sync iptal edildi (kapatma).");
        }

        _log.LogInformation(
            "R2 sync bitti: yüklendi={Uploaded} atlandı={Skipped} başarısız={Failed}.",
            uploaded, skipped, failed);
    }
}