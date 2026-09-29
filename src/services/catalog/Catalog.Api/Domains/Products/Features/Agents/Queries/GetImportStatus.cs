using Catalog.Api.Import;

namespace Catalog.Api.Domains.Products.Features.Agents.Queries;

// US1/FR-010: import ilerleme + başarısızlık raporu. Global sayım (pending/processed/failed) +
// başarısız satırların {isbn,error} listesi. Salt-okur; admin kararını (yeniden yükle / düzelt) besler.
public static class GetImportStatus
{
    [RequiredScope(AuthorizationScopes.AdminCatalogRead)]
    public record GetImportStatusQuery(Guid UserId);

    public class GetImportStatusResponse
    {
        public int Pending { get; set; }
        public int Processed { get; set; }
        public int Failed { get; set; }
        public List<ImportFailure> Failures { get; set; } = [];
    }

    public record ImportFailure(string Isbn, string? Error);

    public class GetImportStatusQueryHandler(IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<GetImportStatusResponse>> Handle(
            GetImportStatusQuery query, CancellationToken ct)
        {
            var pending = await session.Query<ImportRow>().CountAsync(r => r.Status == ImportRowStatus.Pending, ct);
            var processed = await session.Query<ImportRow>().CountAsync(r => r.Status == ImportRowStatus.Processed, ct);

            // Başarısız satırların sebepleri (rapor) — makul bir tavan (aşırı-büyük listeyi bölme).
            var failed = await session.Query<ImportRow>()
                .Where(r => r.Status == ImportRowStatus.Failed)
                .OrderByDescending(r => r.CreatedTime)
                .Take(200)
                .ToListAsync(ct);
            var failedCount = await session.Query<ImportRow>().CountAsync(r => r.Status == ImportRowStatus.Failed, ct);

            return FeatureObjectResultModel<GetImportStatusResponse>.Ok(new GetImportStatusResponse
            {
                Pending = pending,
                Processed = processed,
                Failed = failedCount,
                Failures = failed.Select(r => new ImportFailure(r.Isbn, r.Error)).ToList()
            });
        }
    }
}

[McpServerToolType]
public static class GetImportStatusMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.GetImportStatus)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.GetImportStatus)]
    public static Task<FeatureObjectResultModel<GetImportStatus.GetImportStatusResponse>> GetImportStatusAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<GetImportStatus.GetImportStatusResponse>>(
            new GetImportStatus.GetImportStatusQuery(userId), ct);
    }
}
