namespace Catalog.Api.Domains.ImportJobs;

/// <summary>
/// Excel import işinin yaşam döngüsü (004, iki-faz): Queued doğar → dry-run (Validating →
/// AwaitingConfirmation, rapor birikir ama HİÇ yazılmaz) → admin onayı (Confirmed) → gerçek
/// yazım (Running → Completed). Start onayda dry-run sayaçlarını sıfırlar — rapor gerçek koşuda
/// yeniden üretilir. Terminal (Completed/Failed) sonrası mutasyon reddedilir; geriye geçiş yok.
/// "Aynı anda tek aktif iş" kuralı handler'da sorguyla uygulanır (FR-008) — aggregate kendi
/// dışını göremez.
/// </summary>
public class ImportJob : AggregateRoot
{
    public ImportJobStatus Status { get; private set; }
    public string FileName { get; private set; } = default!;
    // Geçici disk yolu — iş bitince süreç dosyayı siler; kalıcı veri değil, iz.
    public string TempFilePath { get; private set; } = default!;

    public int Processed { get; private set; }
    public int Added { get; private set; }
    public int Skipped { get; private set; }
    public int Errored => _errors.Count;

    private readonly List<ImportError> _errors = new();
    public IReadOnlyList<ImportError> Errors => _errors;

    public string? FailureReason { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    private ImportJob()
    {
    }

    // JasperFxIgnore: statik Create fabrikası, event-sourcing evolver konvansiyonuyla çakışır;
    // bu bir domain fabrikasıdır, projection değil (source generator'ı devre dışı bırakır).
    /// <summary>Yeni import işi — Queued doğar. Dosya adı ve geçici yol boş olamaz.</summary>
    [JasperFx.Core.JasperFxIgnore]
    public static ResultDomain<ImportJob> Create(string fileName, string tempFilePath)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return ResultDomain<ImportJob>.Error(new MessageItem
            { Property = nameof(FileName), Code = CatalogResourceConstants.VALUE_EMPTY });
        if (string.IsNullOrWhiteSpace(tempFilePath))
            return ResultDomain<ImportJob>.Error(new MessageItem
            { Property = nameof(TempFilePath), Code = CatalogResourceConstants.VALUE_EMPTY });

        return ResultDomain<ImportJob>.Ok(new ImportJob
        {
            Status = ImportJobStatus.Queued,
            FileName = fileName.Trim(),
            TempFilePath = tempFilePath,
        });
    }

    /// <summary>Dry-run'ı başlatır: yalnız Queued→Validating.</summary>
    public ResultDomain StartValidation()
    {
        if (Status != ImportJobStatus.Queued)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Status), Code = CatalogResourceConstants.IMPORT_STATE_INVALID });
        Status = ImportJobStatus.Validating;
        return ResultDomain.Ok();
    }

    /// <summary>Dry-run'ı kapatır: yalnız Validating→AwaitingConfirmation; rapor onaya hazır.</summary>
    public ResultDomain CompleteValidation()
    {
        if (Status != ImportJobStatus.Validating)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Status), Code = CatalogResourceConstants.IMPORT_STATE_INVALID });
        Status = ImportJobStatus.AwaitingConfirmation;
        return ResultDomain.Ok();
    }

    /// <summary>Admin onayı: yalnız AwaitingConfirmation→Confirmed; gerçek yazım bundan sonra başlatılabilir.</summary>
    public ResultDomain Confirm()
    {
        if (Status != ImportJobStatus.AwaitingConfirmation)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Status), Code = CatalogResourceConstants.IMPORT_STATE_INVALID });
        Status = ImportJobStatus.Confirmed;
        return ResultDomain.Ok();
    }

    /// <summary>Gerçek yazımı başlatır: yalnız Confirmed→Running; StartedAt damgalanır.
    /// Dry-run sayaç/hataları sıfırlanır — rapor gerçek koşuda yeniden üretilir.</summary>
    public ResultDomain Start()
    {
        if (Status != ImportJobStatus.Confirmed)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Status), Code = CatalogResourceConstants.IMPORT_STATE_INVALID });
        Status = ImportJobStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
        Processed = 0;
        Added = 0;
        Skipped = 0;
        _errors.Clear();
        return ResultDomain.Ok();
    }

    /// <summary>Sayaç deltalarını biriktirir (Validating=dry-run veya Running=gerçek; negatif delta
    /// reddedilir). Hatalar sebepli listeye eklenir — iş durmaz (FR-007).</summary>
    public ResultDomain RecordProgress(int processed, int added, int skipped, IEnumerable<ImportError> errors)
    {
        if (Status is not (ImportJobStatus.Validating or ImportJobStatus.Running))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Status), Code = CatalogResourceConstants.IMPORT_STATE_INVALID });
        if (processed < 0 || added < 0 || skipped < 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Processed), Code = CatalogResourceConstants.VALUE_INVALID });

        Processed += processed;
        Added += added;
        Skipped += skipped;
        _errors.AddRange(errors);
        return ResultDomain.Ok();
    }

    /// <summary>İşi tamamlar: yalnız Running→Completed; FinishedAt damgalanır.</summary>
    public ResultDomain Complete()
    {
        if (Status != ImportJobStatus.Running)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Status), Code = CatalogResourceConstants.IMPORT_STATE_INVALID });
        Status = ImportJobStatus.Completed;
        FinishedAt = DateTimeOffset.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>İşi başarısız kapatır (Queued/Running→Failed; sebep zorunlu). Terminalden sonra reddedilir.</summary>
    public ResultDomain Fail(string reason)
    {
        if (Status is ImportJobStatus.Completed or ImportJobStatus.Failed)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Status), Code = CatalogResourceConstants.IMPORT_STATE_INVALID });
        if (string.IsNullOrWhiteSpace(reason))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(FailureReason), Code = CatalogResourceConstants.VALUE_EMPTY });

        Status = ImportJobStatus.Failed;
        FailureReason = reason.Trim();
        FinishedAt = DateTimeOffset.UtcNow;
        return ResultDomain.Ok();
    }
}

// Enum aggregate dosyasında yaşar (konvansiyon). Yeni değerler SONA eklenir —
// sayısal değerler Marten'da saklı belgelerle eşleşir, yeniden sıralama bozar.
public enum ImportJobStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Validating = 4,
    AwaitingConfirmation = 5,
    Confirmed = 6,
}
