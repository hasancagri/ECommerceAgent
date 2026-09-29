namespace Discount.Api.Domains.Campaigns.Features.Agents.Queries;

// US1: admin kampanya görünürlüğü (liste). Opsiyonel status süzgeci. Boş liste = Ok (NotFound değil —
// legit "kampanya yok" durumu); Response sarmalayıcı kullanılır.
public static class ListCampaigns
{
    [RequiredScope(AuthorizationScopes.AdminDiscountWrite)]
    public record ListCampaignsQuery(CampaignStatus? Status);

    public class ListCampaignsResponse
    {
        public List<CampaignSummary> Campaigns { get; set; } = [];
    }

    public class CampaignSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string ScopeType { get; set; } = default!;
        public Guid ScopeRef { get; set; }
        public int Percentage { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }
        public string Status { get; set; } = default!;
    }

    public class ListCampaignsQueryHandler(IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<ListCampaignsResponse>> Handle(
            ListCampaignsQuery query, CancellationToken ct)
        {
            var q = session.Query<Campaign>().AsQueryable();
            if (query.Status is { } status)
                q = q.Where(c => c.Status == status);

            var campaigns = await q.OrderByDescending(c => c.CreatedTime).ToListAsync(ct);

            return FeatureObjectResultModel<ListCampaignsResponse>.Ok(new ListCampaignsResponse
            {
                Campaigns = campaigns.Select(c => new CampaignSummary
                {
                    Id = c.Id,
                    Name = c.Name,
                    ScopeType = c.ScopeType.ToString(),
                    ScopeRef = c.ScopeRef,
                    Percentage = c.Percentage,
                    StartsAt = c.StartsAt,
                    EndsAt = c.EndsAt,
                    Status = c.Status.ToString()
                }).ToList()
            });
        }
    }
}

[McpServerToolType]
public static class ListCampaignsMcpTool
{
    [McpServerTool(Name = Shared.DiscountAdminTools.ListCampaigns)]
    [Description(Shared.McpToolDescriptions.DiscountAdminTools.ListCampaigns)]
    public static Task<FeatureObjectResultModel<ListCampaigns.ListCampaignsResponse>> ListCampaignsAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Opsiyonel durum süzgeci: Scheduled|Active|Ended|Cancelled; boş = hepsi")] string? status = null)
    {
        CampaignStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CampaignStatus>(status, ignoreCase: true, out var s))
            parsed = s;

        return bus.InvokeAsync<FeatureObjectResultModel<ListCampaigns.ListCampaignsResponse>>(
            new ListCampaigns.ListCampaignsQuery(parsed), ct);
    }
}
