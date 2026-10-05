namespace Customer.Api.Domains.MerchantInformations.Features.Agents.Commands;

// 087 US1/FR-002: makine-handoff merchant kaydı — store-başlatır. Admin MCP aksiyonu tetikler; store
// correlationId üretir, MerchantInformation'ı Pending'e alır, PG'ye S2S register isteği atar (bootstrap
// key + callbackUrl gövdede/başlıkta). Finansal/sır bilgi MCP arg'ına girmez. Yanıt sır-free: yalnız
// durum (Pending) — MerchantId/MerchantKey DÖNMEZ (onayda HMAC-callback'le gelir). (078 hosted form SÖKÜLDÜ.)
public static class AdminStartOnboarding
{
    [RequiredScope(AuthorizationScopes.MerchantCredentialsWrite)]
    public record AdminStartOnboardingCommand(Guid UserId, string Email);

    public class AdminStartOnboardingResponse
    {
        public string ApplicationStatus { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    [Transactional]
    public class AdminStartOnboardingCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminStartOnboardingResponse>> Handle(
            AdminStartOnboardingCommand cmd,
            IDocumentSession session,
            Onboarding.PgOnboardingClient gateway,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Email))
                return FeatureObjectResultModel<AdminStartOnboardingResponse>.Error(new MessageItem
                { Property = nameof(cmd.Email), Code = CustomerResourceConstants.VALUE_IS_REQUIRED });

            if (!gateway.IsConfigured)
                return FeatureObjectResultModel<AdminStartOnboardingResponse>.Error(new MessageItem
                { Code = CustomerResourceConstants.MERCHANT_ONBOARDING_UNAVAILABLE });

            // Store tek-merchant: kayıt yoksa tohum üret; varsa mevcut kayıt üstünden yeni çevrim.
            var info = await session.Query<MerchantInformation>().FirstOrDefaultAsync(ct)
                       ?? MerchantInformation.NewUnregistered();

            var correlationId = Guid.NewGuid();
            // Guard (tek-aktif kayıt) PG'ye gitmeden önce: zaten Pending + farklı correlation ise reddet.
            var started = info.StartRegistration(correlationId);
            if (!started.IsSuccess)
                return FeatureObjectResultModel<AdminStartOnboardingResponse>.Error(started.Messages);

            // PG'ye S2S register (bootstrap key + callbackUrl gövdede). null = PG erişilemez → fail-closed,
            // hiçbir şey persist edilmez (info henüz Store edilmedi).
            var accepted = await gateway.RegisterAsync(cmd.Email.Trim(), correlationId, ct);
            if (accepted is null)
                return FeatureObjectResultModel<AdminStartOnboardingResponse>.Error(new MessageItem
                { Code = CustomerResourceConstants.MERCHANT_ONBOARDING_UNAVAILABLE });

            session.Store(info);

            return FeatureObjectResultModel<AdminStartOnboardingResponse>.Ok(new AdminStartOnboardingResponse
            {
                ApplicationStatus = accepted.Status,
                Message = "Kayit baslatildi (Pending). PG admini onaylayinca merchant kimligi store'a " +
                          "guvenli sekilde otomatik gelir; durumu admin_onboarding_status ile izleyin."
            });
        }
    }
}

[McpServerToolType]
public static class AdminStartOnboardingMcpTool
{
    [McpServerTool(Name = Shared.CustomerAdminTools.StartOnboarding)]
    [Description(Shared.McpToolDescriptions.CustomerAdminTools.StartOnboarding)]
    public static Task<FeatureObjectResultModel<AdminStartOnboarding.AdminStartOnboardingResponse>> AdminStartOnboardingAsync(
        [Description("Basvuru sahibinin e-postasi (basvuru kimligi)")] string email,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminStartOnboarding.AdminStartOnboardingResponse>>(
            new AdminStartOnboarding.AdminStartOnboardingCommand(userId, email), ct);
    }
}