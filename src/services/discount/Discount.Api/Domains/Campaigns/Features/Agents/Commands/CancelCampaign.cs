namespace Discount.Api.Domains.Campaigns.Features.Agents.Commands;

// US1/edge: admin kampanyayı iptal eder → Campaign.Cancel + o kampanyanın ProductDiscount'ları silinir
// + ProductDiscountChanged(pct:0) push (Storefront liste fiyatına döner). Scheduled end-fire sonradan
// gelse bile no-op (satır yok — idempotent).
public static class CancelCampaign
{
    [RequiredScope(AuthorizationScopes.AdminDiscountWrite)]
    public record CancelCampaignCommand(Guid CampaignId);

    public class CancelCampaignResponse
    {
        public Guid CampaignId { get; set; }
        public string Message { get; set; } = default!;
    }

    [Transactional]
    public class CancelCampaignCommandHandler(IDocumentSession session, IMessageBus bus)
    {
        public async Task<FeatureObjectResultModel<CancelCampaignResponse>> Handle(
            CancelCampaignCommand cmd, CancellationToken ct)
        {
            var campaign = await session.LoadAsync<Campaign>(cmd.CampaignId, ct);
            if (campaign is null)
                return FeatureObjectResultModel<CancelCampaignResponse>.Error(
                    new MessageItem { Code = DiscountResourceConstants.CAMPAIGN_NOT_FOUND });

            var cancel = campaign.Cancel();
            if (!cancel.IsSuccess)
                return FeatureObjectResultModel<CancelCampaignResponse>.Error(cancel.Messages);

            session.Store(campaign);
            await CampaignApplication.ClearAsync(campaign.Id, session, bus, ct);

            return FeatureObjectResultModel<CancelCampaignResponse>.Ok(new CancelCampaignResponse
            {
                CampaignId = campaign.Id,
                Message = "Kampanya iptal edildi; kitapların indirimi temizlendi."
            });
        }
    }
}

[McpServerToolType]
public static class CancelCampaignMcpTool
{
    [McpServerTool(Name = Shared.DiscountAdminTools.CancelCampaign)]
    [Description(Shared.McpToolDescriptions.DiscountAdminTools.CancelCampaign)]
    public static Task<FeatureObjectResultModel<CancelCampaign.CancelCampaignResponse>> CancelCampaignAsync(
        [Description("İptal edilecek kampanya id'si")] Guid campaignId,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<CancelCampaign.CancelCampaignResponse>>(
            new CancelCampaign.CancelCampaignCommand(campaignId), ct);
}
