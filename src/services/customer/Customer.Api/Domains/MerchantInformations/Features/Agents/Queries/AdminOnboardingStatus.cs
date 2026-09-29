namespace Customer.Api.Domains.MerchantInformations.Features.Agents.Queries;

// US4: DropShop onboarding DURUM sorgusu — S2S REST (PgOnboardingClient, kontrat #2).
// Yanıtta MerchantId/MerchantKey YOKTUR (FR-008): teslim yolu mail + tek gösterimlik link +
// store credential ekranıdır; Approved mesajı admini o yola yönlendirir. Okuma — iz yazılmaz (FR-009).
public static class AdminOnboardingStatus
{
    [RequiredScope(AuthorizationScopes.MerchantCredentialsWrite)]
    public record AdminOnboardingStatusQuery(string Email);

    public class AdminOnboardingStatusResponse
    {
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? RejectReason { get; set; }
    }

    public class AdminOnboardingStatusQueryHandler
    {
        public async Task<FeatureObjectResultModel<AdminOnboardingStatusResponse>> Handle(
            AdminOnboardingStatusQuery query,
            Onboarding.PgOnboardingClient gateway,
            CancellationToken ct)
        {
            var status = await gateway.GetStatusAsync(query.Email, ct);
            if (status is null)
                return FeatureObjectResultModel<AdminOnboardingStatusResponse>.Error(new MessageItem
                { Code = CustomerResourceConstants.MERCHANT_ONBOARDING_UNAVAILABLE });

            var message = status.Status == "Approved"
                ? "Başvuru onaylandı. Erişim bilgileri başvuru e-postasına gönderilen tek gösterimlik " +
                  "bağlantıda; kaydetmek için admin_request_credential_entry_link ile store ekranını kullanın."
                : status.Message ?? string.Empty;

            return FeatureObjectResultModel<AdminOnboardingStatusResponse>.Ok(new AdminOnboardingStatusResponse
            {
                Status = status.Status,
                Message = message,
                RejectReason = status.RejectReason
            });
        }
    }
}

[McpServerToolType]
public static class AdminOnboardingStatusMcpTool
{
    [McpServerTool(Name = Shared.CustomerAdminTools.OnboardingStatus)]
    [Description(Shared.McpToolDescriptions.CustomerAdminTools.OnboardingStatus)]
    public static Task<FeatureObjectResultModel<AdminOnboardingStatus.AdminOnboardingStatusResponse>> AdminOnboardingStatusAsync(
        [Description("Basvuruda kullanilan e-posta")] string email,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminOnboardingStatus.AdminOnboardingStatusResponse>>(
            new AdminOnboardingStatus.AdminOnboardingStatusQuery(email), ct);
}
