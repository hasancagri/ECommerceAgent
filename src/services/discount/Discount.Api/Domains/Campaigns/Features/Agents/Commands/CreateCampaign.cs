using Discount.Api.Process;

namespace Discount.Api.Domains.Campaigns.Features.Agents.Commands;

// 079 US1/US2: admin süzgeçle kampanya açar. Campaign.Create → (aktifse) süzgeci kitap setine çöz +
// her kitaba ProductDiscount YAZ (varsa üzerine yaz — son-gelen-kazanır) + ProductDiscountChanged push;
// gelecek tarihli ise ProductDiscount YAZMA (Scheduled). Her iki halde start/end scheduled message kurulur
// (durable süre yönetimi). Yanıt kısa özet {applied, scheduled}.
public static class CreateCampaign
{
    [RequiredScope(AuthorizationScopes.AdminDiscountWrite)]
    public record CreateCampaignCommand(
        string Name, ScopeType ScopeType, Guid ScopeRef, int Percentage, DateTime StartsAt, DateTime? EndsAt);

    public class CreateCampaignResponse
    {
        public Guid CampaignId { get; set; }
        public bool Scheduled { get; set; }
        public int Applied { get; set; }
        public int Skipped { get; set; }
        public string Message { get; set; } = default!;
    }

    [Transactional]
    public class CreateCampaignCommandHandler(IDocumentSession session, IMessageBus bus)
    {
        public async Task<FeatureObjectResultModel<CreateCampaignResponse>> Handle(
            CreateCampaignCommand cmd, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var create = Campaign.Create(
                cmd.Name, cmd.ScopeType, cmd.ScopeRef, cmd.Percentage, cmd.StartsAt, cmd.EndsAt, now);
            if (!create.IsSuccess)
                return FeatureObjectResultModel<CreateCampaignResponse>.Error(create.Messages);

            var campaign = create.Data!;
            session.Store(campaign);

            var response = new CreateCampaignResponse { CampaignId = campaign.Id };

            if (campaign.IsEffectiveAt(now))
            {
                // Aktif doğar → hemen uygula (resolve + apply-skip + push).
                var (applied, skipped) = await CampaignApplication.ActivateAsync(campaign, session, bus, ct);
                response.Applied = applied;
                response.Skipped = skipped;
                response.Scheduled = false;
                response.Message = $"Kampanya aktif: {applied} kitap indirimli (son-gelen-kazanır; varsa üzerine yazıldı).";
            }
            else
            {
                // Gelecek tarihli → ProductDiscount YAZMA; start-fire aktifleştirir.
                await bus.ScheduleAsync(new CampaignActivated(campaign.Id), campaign.StartsAt - now);
                response.Scheduled = true;
                response.Message = $"Kampanya {campaign.StartsAt:u} için zamanlandı.";
            }

            // Bitiş dayanıklı zamanlama (varsa) — fire kampanyanın kitaplarını temizler.
            if (campaign.EndsAt.HasValue)
                await bus.ScheduleAsync(new CampaignEnded(campaign.Id), campaign.EndsAt.Value - now);

            return FeatureObjectResultModel<CreateCampaignResponse>.Ok(response);
        }
    }
}

[McpServerToolType]
public static class CreateCampaignMcpTool
{
    [McpServerTool(Name = Shared.DiscountAdminTools.CreateCampaign)]
    [Description(Shared.McpToolDescriptions.DiscountAdminTools.CreateCampaign)]
    public static async Task<FeatureObjectResultModel<CreateCampaign.CreateCampaignResponse>> CreateCampaignAsync(
        [Description("Kampanya adı (admin etiketi)")] string name,
        [Description("Süzgeç tipi: category | author | publisher | product")] string scopeType,
        [Description("Süzgeç referansı: kategori/yazar/yayınevi id'si ya da tek-kitapta ürün id'si")] Guid scopeRef,
        [Description("İndirim yüzdesi (1-99)")] int percentage,
        IMessageBus bus,
        CancellationToken ct,
        [Description("Başlangıç (ISO-8601 UTC); boş = şimdi")] DateTime? startsAt = null,
        [Description("Bitiş (ISO-8601 UTC); boş = süresiz")] DateTime? endsAt = null)
    {
        if (!Enum.TryParse<ScopeType>(scopeType, ignoreCase: true, out var parsedScope))
            return FeatureObjectResultModel<CreateCampaign.CreateCampaignResponse>.Error(
                new MessageItem { Code = DiscountResourceConstants.CAMPAIGN_SCOPE_TYPE_INVALID, Property = scopeType });

        return await bus.InvokeAsync<FeatureObjectResultModel<CreateCampaign.CreateCampaignResponse>>(
            new CreateCampaign.CreateCampaignCommand(
                name, parsedScope, scopeRef, percentage, startsAt ?? DateTime.UtcNow, endsAt), ct);
    }
}
