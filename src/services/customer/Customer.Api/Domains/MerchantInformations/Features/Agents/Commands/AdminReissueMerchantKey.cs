namespace Customer.Api.Domains.MerchantInformations.Features.Agents.Commands;

// 080/PG-046: merchant kaybettiği/sızdığından şüphelendiği MerchantKey yerine taze key alır. Store
// kayıtlı MerchantId ile PG'ye reissue tetikler (merchant KEY'i kaybetti, Id'yi değil); PG eski key'i
// her temsilde anında öldürür + yeni key'i tek gösterimlik reveal linkiyle sunar. Yanıt YALNIZ reveal
// URL (key sohbete/store'a girmez — FR-005). Yeni key'in store'a yazımı mevcut credential-giriş
// yoluyla (admin_request_credential_entry_link → SubmitMerchantCredentials, PG doğrulamalı UpdateKey).
public static class AdminReissueMerchantKey
{
    [RequiredScope(AuthorizationScopes.MerchantCredentialsWrite)]
    public record AdminReissueMerchantKeyCommand(Guid UserId, string? Reason);

    public class AdminReissueMerchantKeyResponse
    {
        public string RevealUrl { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class AdminReissueMerchantKeyCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminReissueMerchantKeyResponse>> Handle(
            AdminReissueMerchantKeyCommand cmd,
            IDocumentSession session,
            Onboarding.PgOnboardingClient gateway,
            CancellationToken ct)
        {
            // Store tek-merchant: kayıtlı MerchantInformation'dan MerchantId alınır (key kayıp, Id değil).
            var info = await session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);
            if (info is null)
                return FeatureObjectResultModel<AdminReissueMerchantKeyResponse>.Error(new MessageItem
                { Code = CustomerResourceConstants.RECORD_NOT_FOUND });

            var reissue = gateway.IsConfigured
                ? await gateway.ReissueAsync(info.MerchantId, cmd.Reason?.Trim(), ct)
                : null;

            // PG erişilemez / config yok / merchant Active değil → dostane hata, teknik detay sızmaz.
            if (reissue is null)
                return FeatureObjectResultModel<AdminReissueMerchantKeyResponse>.Error(new MessageItem
                { Code = CustomerResourceConstants.MERCHANT_ONBOARDING_UNAVAILABLE });

            return FeatureObjectResultModel<AdminReissueMerchantKeyResponse>.Ok(new AdminReissueMerchantKeyResponse
            {
                RevealUrl = reissue.RevealUrl,
                ExpiresAt = reissue.ExpiresAt,
                Message = "Yeni key hazir; reveal linkini merchant'a iletin — key BIR KEZ gosterilir. " +
                          "Merchant key'i okuduktan sonra admin_request_credential_entry_link ile store'a " +
                          "elle girer (eski key artik gecersizdir)."
            });
        }
    }
}

[McpServerToolType]
public static class AdminReissueMerchantKeyMcpTool
{
    [McpServerTool(Name = Shared.CustomerAdminTools.ReissueMerchantKey)]
    [Description(Shared.McpToolDescriptions.CustomerAdminTools.ReissueMerchantKey)]
    public static Task<FeatureObjectResultModel<AdminReissueMerchantKey.AdminReissueMerchantKeyResponse>> AdminReissueMerchantKeyAsync(
        [Description("Yenileme nedeni (opsiyonel): ornegin 'unuttum' veya 'sizinti-suphesi'")] string? reason,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminReissueMerchantKey.AdminReissueMerchantKeyResponse>>(
            new AdminReissueMerchantKey.AdminReissueMerchantKeyCommand(userId, reason), ct);
    }
}
