using Catalog.Api.Domains.ImportJobs;

namespace Catalog.Api.Domains.ImportJobs.Features.Queries;

// 004 US1: sebepli hata listesi (FR-005). Rapor iş bittikten sonra da erişilebilir kalır.
public static class GetImportErrors
{
    public record GetImportErrorsQuery(Guid JobId);

    public class ImportErrorView
    {
        public string Sheet { get; set; } = default!;
        public int Row { get; set; }
        public string Reason { get; set; } = default!;
    }

    public class GetImportErrorsQueryHandler(IQuerySession session)
    {
        public async Task<FeatureListResultModel<ImportErrorView>> Handle(GetImportErrorsQuery query, CancellationToken ct)
        {
            var job = await session.LoadAsync<ImportJob>(query.JobId, ct);
            if (job is null)
                return FeatureListResultModel<ImportErrorView>.NotFound();

            var views = job.Errors
                .Select(e => new ImportErrorView { Sheet = e.Sheet, Row = e.Row, Reason = e.Reason })
                .ToList();
            return FeatureListResultModel<ImportErrorView>.Ok(views);
        }
    }
}
