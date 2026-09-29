namespace Customer.Api.Onboarding;

// D3: Store → PG (DropShop Merchant.Api) onboarding S2S REST istemcisi — 070'in imperatif MCP
// sapmasının (MerchantOnboardingClient) yerini alır; kontrat specs/078/contracts/pg-onboarding-rest.md.
// Auth: makine kimliği (OnboardingGatewayTokenHandler, client_credentials); admin kullanıcı token'ı
// dış realm'e gitmez. Ulaşım/protokol hatasında null döner — çağıran dostane "şu an yapılamıyor"
// üretir, teknik detay sızmaz (FR-011).
public sealed class PgOnboardingClient(
    HttpClient http,
    DropShopOnboardingOption option,
    ILogger<PgOnboardingClient> logger)
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

    public bool IsConfigured => option.IsConfigured;

    // Kontrat #1 yanıtı: Pending-varken formUrl null + applicationStatus="Pending" gelir.
    public sealed record OnboardingSession(string? FormUrl, DateTimeOffset? ExpiresAt, string ApplicationStatus);

    // Kontrat #2 yanıtı: 404 yerine status="None" döner; MerchantId/MerchantKey ASLA yer almaz.
    public sealed record ApplicationStatus(string Status, string? Message, string? RejectReason);

    private sealed record CreateSessionRequest(string Email);
    private sealed record ValidateRequest(Guid MerchantId, string MerchantKey);
    private sealed record ValidateReply(bool Valid);
    private sealed record ReissueRequest(Guid MerchantId, string? Reason);

    // PG 046 kontrat yanıtı: yeni key GÖVDEDE gelmez — yalnız tek gösterimlik reveal URL + expiry.
    public sealed record ReissueResult(string RevealUrl, DateTimeOffset ExpiresAt);

    // PG 046 — POST /api/v1/onboarding/reissue: merchant kaybettiği/sızdığından şüphelendiği key
    // yerine taze key alır; eski key PG'de her temsilde anında ölür. null = PG erişilemedi.
    public Task<ReissueResult?> ReissueAsync(Guid merchantId, string? reason, CancellationToken ct) =>
        SendAsync<ReissueResult>(
            () => new HttpRequestMessage(HttpMethod.Post, Url("/api/v1/onboarding/reissue"))
            { Content = JsonContent.Create(new ReissueRequest(merchantId, reason)) },
            "onboarding reissue", ct);

    // Kontrat #1 — POST /api/v1/onboarding/sessions: PG'de hosted form oturumu açar.
    public Task<OnboardingSession?> CreateSessionAsync(string email, CancellationToken ct) =>
        SendAsync<OnboardingSession>(
            () => new HttpRequestMessage(HttpMethod.Post, Url("/api/v1/onboarding/sessions"))
            { Content = JsonContent.Create(new CreateSessionRequest(email)) },
            "onboarding session", ct);

    // Kontrat #2 — GET /api/v1/onboarding/applications/{email}: başvuru durumu.
    public Task<ApplicationStatus?> GetStatusAsync(string email, CancellationToken ct) =>
        SendAsync<ApplicationStatus>(
            () => new HttpRequestMessage(HttpMethod.Get,
                Url($"/api/v1/onboarding/applications/{Uri.EscapeDataString(email)}")),
            "onboarding status", ct);

    // Kontrat #3 — POST /api/v1/onboarding/credentials/validate: ikili geçerli mi (FR-013).
    // null = PG erişilemedi (çağıran kaydı CredentialsVerified:false ile saklar).
    public async Task<bool?> ValidateCredentialsAsync(Guid merchantId, string merchantKey, CancellationToken ct)
    {
        var reply = await SendAsync<ValidateReply>(
            () => new HttpRequestMessage(HttpMethod.Post, Url("/api/v1/onboarding/credentials/validate"))
            { Content = JsonContent.Create(new ValidateRequest(merchantId, merchantKey)) },
            "credential validate", ct);
        return reply?.Valid;
    }

    private string Url(string path) => $"{option.ApiBaseUrl.TrimEnd('/')}{path}";

    private async Task<T?> SendAsync<T>(Func<HttpRequestMessage> requestFactory, string operation, CancellationToken ct)
        where T : class
    {
        if (!IsConfigured)
            return null;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(CallTimeout);

            using var request = requestFactory();
            using var response = await http.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("PG onboarding '{Operation}' {StatusCode} dondu.", operation, (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "PG onboarding '{Operation}' cagrisi basarisiz.", operation);
            return null;
        }
    }
}