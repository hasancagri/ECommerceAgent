using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FileApi.Migration;

// US4: R2 bucket'taki mevcut kapakları (fiziki var) kayıt defterine idempotent alır. ListObjectsV2
// ile bucket taranır (per-obje HEAD YOK; Size listeden, ContentType varsayılan). Her key(=ISBN) için
// FileAsset upsert (yok: Create / var: R2 konumu AddOrReplaceLocation). Re-run yinelemez (ImageName merge).
// Config-gated (RegistryBackfill:Enabled) + yalnız Backend=R2. R2SyncHostedService deseni.
public sealed class RegistryBackfillHostedService : BackgroundService
{
    private readonly CoverStoreOptions _store;
    private readonly CoverMigrationOptions _migration;
    private readonly R2Options _r2;
    private readonly IDocumentStore _documentStore;
    private readonly IAmazonS3 _s3;
    private readonly CoverUrlResolver _resolver;
    private readonly ILogger<RegistryBackfillHostedService> _log;

    public RegistryBackfillHostedService(
        CoverStoreOptions store,
        CoverMigrationOptions migration,
        R2Options r2,
        IDocumentStore documentStore,
        IAmazonS3 s3,
        CoverUrlResolver resolver,
        ILogger<RegistryBackfillHostedService> log)
    {
        _store = store;
        _migration = migration;
        _r2 = r2;
        _documentStore = documentStore;
        _s3 = s3;
        _resolver = resolver;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_migration.RegistryBackfill.Enabled || _store.Backend != CoverStoreBackend.R2)
            return;

        if (string.IsNullOrWhiteSpace(_r2.BucketName))
        {
            _log.LogWarning("Registry backfill atlandı: R2 BucketName boş.");
            return;
        }

        var limit = _migration.RegistryBackfill.Limit;
        var defaultContentType = _migration.RegistryBackfill.DefaultContentType;
        int written = 0, skipped = 0, failed = 0, processed = 0;

        _log.LogInformation("Registry backfill başladı (bucket={Bucket} limit={Limit}).", _r2.BucketName, limit);

        await using var session = _documentStore.LightweightSession();
        string? continuationToken = null;

        try
        {
            do
            {
                var listResponse = await _s3.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = _r2.BucketName,
                    ContinuationToken = continuationToken
                }, stoppingToken);

                foreach (var obj in listResponse.S3Objects)
                {
                    if (limit > 0 && processed >= limit) break;
                    processed++;

                    var isbn = obj.Key;
                    if (!CoverKey.TryCreate(isbn, out var key))
                    {
                        failed++;
                        continue;
                    }

                    try
                    {
                        var existing = await session.Query<FileAsset>()
                            .FirstOrDefaultAsync(a => a.ImageName == isbn, stoppingToken);

                        if (existing is null)
                        {
                            var created = FileAsset.Create(isbn, defaultContentType, obj.Size ?? 0, StorageType.R2, key);
                            if (!created.IsSuccess) { failed++; continue; }
                            session.Store(created.Data!);
                            written++;
                        }
                        else
                        {
                            // Zaten R2 konumu varsa upsert path'i aynı → idempotent no-op; yoksa ekler.
                            var alreadyR2 = existing.Locations.Any(l => l.StorageType == StorageType.R2 && l.StorageFilePath == key);
                            if (alreadyR2) { skipped++; continue; }

                            existing.AddOrReplaceLocation(StorageType.R2, key);
                            session.Store(existing);
                            written++;
                        }

                        // Periyodik commit (bellek + tek dev-transaction şişmesini önle).
                        if (written % 500 == 0 && written > 0)
                        {
                            await session.SaveChangesAsync(stoppingToken);
                            _log.LogInformation(
                                "Registry backfill ilerleme: işlenen={Processed} yazıldı={Written} atlandı={Skipped} başarısız={Failed}.",
                                processed, written, skipped, failed);
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _log.LogWarning(ex, "Registry backfill kaydı başarısız (isbn={Isbn}).", isbn);
                    }
                }

                continuationToken = (listResponse.IsTruncated ?? false) ? listResponse.NextContinuationToken : null;
                if (limit > 0 && processed >= limit) break;
            }
            while (continuationToken is not null && !stoppingToken.IsCancellationRequested);

            await session.SaveChangesAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _log.LogWarning("Registry backfill iptal edildi (kapatma).");
            return;
        }

        _log.LogInformation(
            "Registry backfill bitti: yazıldı={Written} atlandı={Skipped} başarısız={Failed}.",
            written, skipped, failed);
    }
}