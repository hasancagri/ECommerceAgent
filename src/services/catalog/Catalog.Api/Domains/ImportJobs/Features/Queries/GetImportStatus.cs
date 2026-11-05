using Catalog.Api.Domains.ImportJobs;

namespace Catalog.Api.Domains.ImportJobs.Features.Queries;

// 004 US1: iş durumu + sayaçlar (FR-006). Cache'LENMEZ — yazma yolunu izleyen canlı ilerleme sorgusu.
public static class GetImportStatus
{
    public record GetImportStatusQuery(Guid JobId);

    public class ImportStatusResponse
    {
        public Guid JobId { get; set; }
        public string Status { get; set; } = default!;
        public string FileName { get; set; } = default!;
        public int Processed { get; set; }
        public int Added { get; set; }
        public int Skipped { get; set; }
        public int Errored { get; set; }
        public string? FailureReason { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
    }

    public class GetImportStatusQueryHandler(IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<ImportStatusResponse>> Handle(GetImportStatusQuery query, CancellationToken ct)
        {
            var job = await session.LoadAsync<ImportJob>(query.JobId, ct);
            if (job is null)
                return FeatureObjectResultModel<ImportStatusResponse>.NotFound();

            return FeatureObjectResultModel<ImportStatusResponse>.Ok(new ImportStatusResponse
            {
                JobId = job.Id,
                Status = job.Status.ToString(),
                FileName = job.FileName,
                Processed = job.Processed,
                Added = job.Added,
                Skipped = job.Skipped,
                Errored = job.Errored,
                FailureReason = job.FailureReason,
                StartedAt = job.StartedAt,
                FinishedAt = job.FinishedAt,
            });
        }
    }
}
