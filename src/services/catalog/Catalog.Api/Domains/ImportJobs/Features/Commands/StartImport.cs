
namespace Catalog.Api.Domains.ImportJobs.Features.Commands;

// 004 US1 (iki-faz, durum-güdümlü): Excel yüklemesini kabul eder — yapı kontrolü (FR-400: sütun),
// tek-aktif-iş kuralı (FR-008), ImportJob doğurur (Queued). TETİK YOK (kullanıcı kararı:
// handler PublishAsync atmaz) — ImportJobWorker Queued'u yoklayıp dry-run'ı kendisi başlatır.
// Yazım dry-run raporu sonrası ConfirmImport onayıyla. Onay bekleyen eski iş yeni yüklemeyle
// geçersiz kılınır (supersede). Satır İŞLEMEZ — o Process/ImportJobWorker'ın işi.
public static class StartImport
{
    public record StartImportCommand(string FileName, string TempFilePath);

    public class StartImportResponse
    {
        public Guid JobId { get; set; }
    }

    public class StartImportCommandHandler(IDocumentSession session)
    {
        [Transactional]
        public async Task<FeatureObjectResultModel<StartImportResponse>> Handle(StartImportCommand cmd, CancellationToken ct)
        {
            // Tek-aktif-iş kuralı (FR-008): kuyrukta/dry-run'da/onaylı/koşan import varken yeni
            // yükleme 409 — iki işin aynı yazar/yayınevi/kategoriyi paralel get-or-create edip çift
            // kayıt doğurması baştan engellenir. Guard handler'da; aggregate kendi dışını göremez.
            var activeExists = await session.Query<ImportJob>()
                .AnyAsync(j => j.Status == ImportJobStatus.Queued
                            || j.Status == ImportJobStatus.Validating
                            || j.Status == ImportJobStatus.Confirmed
                            || j.Status == ImportJobStatus.Running, ct);
            if (activeExists)
                return FeatureObjectResultModel<StartImportResponse>.Error(
                    new MessageItem { Code = CatalogResourceConstants.IMPORT_ALREADY_RUNNING });

            // Onay bekleyen iş engel DEĞİL — yeni yükleme onu geçersiz kılar (yanlış dosya yükledim,
            // doğrusunu attım senaryosu). Dosyalar silinmez (kullanıcı kararı) — diskte iz kalır.
            var awaiting = await session.Query<ImportJob>()
                .Where(j => j.Status == ImportJobStatus.AwaitingConfirmation).ToListAsync(ct);
            foreach (var stale in awaiting)
            {
                stale.Fail("Yeni yüklemeyle geçersiz kılındı.");
                session.Store(stale);
            }

            // Dosya/sütun doğrulama — bozuksa iş hiç doğmaz (FR: 400, hiçbir satır işlenmez).
            var validationError = ExcelWorkbookValidator.Validate(cmd.TempFilePath);
            if (validationError is not null)
                return FeatureObjectResultModel<StartImportResponse>.Error(
                    new MessageItem
                    {
                        Code = CatalogResourceConstants.IMPORT_FILE_INVALID,
                        Params = { validationError },
                    });

            var created = ImportJob.Create(cmd.FileName, cmd.TempFilePath);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<StartImportResponse>.Error(created.Messages);

            var job = created.Data!;
            // Yalnız durum yazılır — Queued kaydı ImportJobWorker'ın sıradaki turunda dry-run'ı başlatır.
            session.Store(job);

            return FeatureObjectResultModel<StartImportResponse>.Ok(new StartImportResponse { JobId = job.Id });
        }
    }
}
