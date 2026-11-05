namespace Catalog.Api.Domains.ImportJobs.Features.Commands;

// 004 (iki-faz, Faz 2 kapısı, durum-güdümlü): admin dry-run raporunu görüp onaylar — iş
// AwaitingConfirmation → Confirmed. TETİK YOK (kullanıcı kararı) — ImportJobWorker Confirmed'i
// yoklayıp yazımı kendisi başlatır. Onaysız yazım yolu YOK. Geçersiz durum (zaten onaylı /
// hiç doğrulanmamış / terminal) hata döner.
public static class ConfirmImport
{
    public record ConfirmImportCommand(Guid JobId);

    public class ConfirmImportResponse
    {
        public Guid JobId { get; set; }
    }

    public class ConfirmImportCommandHandler(IDocumentSession session)
    {
        [Transactional]
        public async Task<FeatureObjectResultModel<ConfirmImportResponse>> Handle(ConfirmImportCommand cmd, CancellationToken ct)
        {
            var job = await session.LoadAsync<ImportJob>(cmd.JobId, ct);
            if (job is null)
                return FeatureObjectResultModel<ConfirmImportResponse>.Error(
                    new MessageItem { Code = CatalogResourceConstants.IMPORT_JOB_NOT_FOUND });

            var confirmed = job.Confirm();
            if (!confirmed.IsSuccess)
                return FeatureObjectResultModel<ConfirmImportResponse>.Error(confirmed.Messages);

            // Yalnız durum yazılır — Confirmed kaydı worker'ın sıradaki turunda yazımı başlatır.
            session.Store(job);

            return FeatureObjectResultModel<ConfirmImportResponse>.Ok(new ConfirmImportResponse { JobId = job.Id });
        }
    }
}
