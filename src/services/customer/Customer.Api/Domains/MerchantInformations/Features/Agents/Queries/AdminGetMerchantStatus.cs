namespace Customer.Api.Domains.MerchantInformations.Features.Agents.Queries;

// 070 US3: merchant kimlik durumu (agent yüzeyi) — GetMerchantInformation İKİZİ (bilinçli tekrar).
// MerchantKey HİÇBİR koşulda dönmez (maskeli durum). Kayıt yoksa configured:false döner (NotFound
// değil — agent "tanımsız" bilgisini düz veri olarak alır). Okuma — iz yazılmaz.
public static class AdminGetMerchantStatus
{
    [RequiredScope(AuthorizationScopes.MerchantCredentialsWrite)]
    public record AdminGetMerchantStatusQuery;

    public class MerchantStatusView
    {
        public bool Configured { get; set; }
        public Guid? MerchantId { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AdminGetMerchantStatusQueryHandler
    {
        public async Task<FeatureObjectResultModel<MerchantStatusView>> Handle(
            AdminGetMerchantStatusQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var merchant = await session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);
            if (merchant is null)
                return FeatureObjectResultModel<MerchantStatusView>.Ok(new MerchantStatusView { Configured = false });

            return FeatureObjectResultModel<MerchantStatusView>.Ok(new MerchantStatusView
            {
                Configured = true,
                MerchantId = merchant.MerchantId,
                UpdatedAt = merchant.UpdatedTime ?? merchant.CreatedTime
            });
        }
    }
}

// 070/085: ADMIN tool'ları — TEK korumalı /mcp ucunda, scope-budamalı yayınlanır (Program.cs oturum filtresi);
// müşteri /mcp tool seti DEĞİŞMEZ (müşteri DCR istemcileri admin şemasını görmez — R1). Kullanıcı
// token'dan; scope katmanı handler'da [RequiredScope(MerchantCredentialsWrite)].
[McpServerToolType]
public static class AdminGetMerchantStatusMcpTool
{
    [McpServerTool(Name = Shared.CustomerAdminTools.GetMerchantStatus)]
    [Description(Shared.McpToolDescriptions.CustomerAdminTools.GetMerchantStatus)]
    public static Task<FeatureObjectResultModel<AdminGetMerchantStatus.MerchantStatusView>> AdminGetMerchantStatusAsync(
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminGetMerchantStatus.MerchantStatusView>>(
            new AdminGetMerchantStatus.AdminGetMerchantStatusQuery(), ct);
}
