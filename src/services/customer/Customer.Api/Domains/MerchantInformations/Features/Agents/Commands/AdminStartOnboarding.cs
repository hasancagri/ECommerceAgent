namespace Customer.Api.Domains.MerchantInformations.Features.Agents.Commands;

// US1/FR-001: PII'siz onboarding başlatma — PG'de hosted form oturumu açtırır, sohbete YALNIZ
// form linki düşer. PII (TCKN/IBAN vb.) PG formunda toplanır; store'a ve LLM'e hiç uğramaz (FR-002).
// Aynı e-postada yaşayan Pending başvuru varsa PG yeni oturum açmaz → formUrl null + dostane mesaj.
public static class AdminStartOnboarding
{
    [RequiredScope(AuthorizationScopes.MerchantCredentialsWrite)]
    public record AdminStartOnboardingCommand(Guid UserId, string Email);

    public class AdminStartOnboardingResponse
    {
        public string? FormUrl { get; set; }
        public string ApplicationStatus { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class AdminStartOnboardingCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminStartOnboardingResponse>> Handle(
            AdminStartOnboardingCommand cmd,
            Onboarding.PgOnboardingClient gateway,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Email))
                return FeatureObjectResultModel<AdminStartOnboardingResponse>.Error(new MessageItem
                { Property = nameof(cmd.Email), Code = CustomerResourceConstants.VALUE_IS_REQUIRED });

            var session = gateway.IsConfigured
                ? await gateway.CreateSessionAsync(cmd.Email.Trim(), ct)
                : null;

            // PG erişilemez / config yok → dostane hata, teknik detay sızmaz (FR-011).
            if (session is null)
                return FeatureObjectResultModel<AdminStartOnboardingResponse>.Error(new MessageItem
                { Code = CustomerResourceConstants.MERCHANT_ONBOARDING_UNAVAILABLE });

            if (session.FormUrl is null)
            {
                return FeatureObjectResultModel<AdminStartOnboardingResponse>.Ok(new AdminStartOnboardingResponse
                {
                    FormUrl = null,
                    ApplicationStatus = session.ApplicationStatus,
                    Message = "Bu e-posta ile bekleyen bir basvuru zaten var; durumu admin_onboarding_status ile takip edin."
                });
            }

            return FeatureObjectResultModel<AdminStartOnboardingResponse>.Ok(new AdminStartOnboardingResponse
            {
                FormUrl = session.FormUrl,
                ApplicationStatus = session.ApplicationStatus,
                Message = "Basvuru formu hazir; linki mustakbel merchant'a iletin, formu PG ekraninda kendisi doldurur."
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