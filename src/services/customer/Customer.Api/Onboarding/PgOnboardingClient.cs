namespace Customer.Api.Onboarding;

// D3: Store → PG (DropShop Merchant.Api) onboarding S2S REST istemcisi — 070'in imperatif MCP
// sapmasının (MerchantOnboardingClient) yerini alır; kontrat specs/078/contracts/pg-onboarding-rest.md.
// Auth: makine kimliği (OnboardingGatewayTokenHandler, client_credentials); admin kullanıcı token'ı
// dış realm'e gitmez. Ulaşım/protokol hatasında null döner — çağıran dostane "şu an yapılamıyor"
// üretir, teknik detay sızmaz (FR-011).
public sealed class PgOnboardingClient(
    HttpClient http,
    DropShopOnboardingOption option,
    MerchantBusinessProfileOption businessProfile,
    ILogger<PgOnboardingClient> logger)
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

    public bool IsConfigured => option.IsConfigured;

    // Kontrat #2 yanıtı: 404 yerine status="None" döner; MerchantId/MerchantKey ASLA yer almaz.
    public sealed record ApplicationStatus(string Status, string? Message, string? RejectReason);

    // 087 register-request kontratı: finansal alan yalnız GÖVDEDE (MCP arg değil, sunucu-tarafı okunur).
    // Alan adları PG BusinessBody ile BİREBİR (case-insensitive JSON); Email = başvuru kimliği (MCP arg);
    // geri kalanı MerchantBusinessProfile secret config'ten (FR-002). Hiçbiri log/trace/dönüşe yazılmaz.
    // Type = PG MerchantType enum değeri (sayısal; PG string converter'ı yok).
    private sealed record RegisterBusiness(
        int Type, string Name, string Email, string GsmNumber, string Address, string Iban,
        string ContactName, string ContactSurname,
        string? IdentityNumber, string? TaxOffice, string? TaxNumber, string? LegalCompanyTitle);
    private sealed record RegisterRequest(Guid CorrelationId, string CallbackUrl, RegisterBusiness Business);
    private sealed record ReissueRequest(Guid CorrelationId, string CallbackUrl, Guid MerchantId, string? Reason);

    // 087 register-request kontratı yanıtı (202): yalnız kabul makbuzu — credential DÖNMEZ (callback'le gelir).
    public sealed record RegisterAccepted(bool Accepted, Guid CorrelationId, string Status);

    // 087 Kontrat #1 — POST /api/v1/onboarding/register: store-başlatır kayıt. X-Registration-Key (bootstrap)
    // başlığı + finansal payload gövdede + callbackUrl/correlationId. 202 kabul; 401/403 = bootstrap geçersiz
    // → null (fail-closed, kayıt başlamaz); null = PG erişilemedi. Credential asenkron callback'le döner.
    public Task<RegisterAccepted?> RegisterAsync(string contactEmail, Guid correlationId, CancellationToken ct) =>
        SendAsync<RegisterAccepted>(
            () => WithBootstrapKey(new HttpRequestMessage(HttpMethod.Post, Url("/api/v1/onboarding/register"))
            {
                Content = JsonContent.Create(new RegisterRequest(correlationId, option.CallbackUrl,
                    new RegisterBusiness(
                        businessProfile.Type, businessProfile.Name, contactEmail, businessProfile.GsmNumber,
                        businessProfile.Address, businessProfile.Iban, businessProfile.ContactName,
                        businessProfile.ContactSurname, businessProfile.IdentityNumber, businessProfile.TaxOffice,
                        businessProfile.TaxNumber, businessProfile.LegalName)))
            }),
            "onboarding register", ct);

    // 087 — POST /api/v1/onboarding/reissue: kayıtlı MerchantId için taze key tetikler; eski key PG'de anında
    // ölür, YENİ key aynı HMAC-callback yoluyla (correlationId) store'a gelir (ekransız — reveal URL YOK).
    public Task<RegisterAccepted?> ReissueAsync(Guid merchantId, string? reason, Guid correlationId, CancellationToken ct) =>
        SendAsync<RegisterAccepted>(
            () => WithBootstrapKey(new HttpRequestMessage(HttpMethod.Post, Url("/api/v1/onboarding/reissue"))
            { Content = JsonContent.Create(new ReissueRequest(correlationId, option.CallbackUrl, merchantId, reason)) }),
            "onboarding reissue", ct);

    // Kontrat #2 — GET /api/v1/onboarding/applications/{email}: başvuru durumu.
    public Task<ApplicationStatus?> GetStatusAsync(string email, CancellationToken ct) =>
        SendAsync<ApplicationStatus>(
            () => new HttpRequestMessage(HttpMethod.Get,
                Url($"/api/v1/onboarding/applications/{Uri.EscapeDataString(email)}")),
            "onboarding status", ct);

    private string Url(string path) => $"{option.ApiBaseUrl.TrimEnd('/')}{path}";

    // Kayıt-ucu yetkisi: m2m bearer (OnboardingGatewayTokenHandler) taşıma kimliği; bootstrap key kayıt-ucunu
    // açar — iki kaygı ayrı başlıkta (FR-003). m2m token handler zinciri bearer'ı ayrıca takar.
    private HttpRequestMessage WithBootstrapKey(HttpRequestMessage request)
    {
        request.Headers.Add("X-Registration-Key", option.BootstrapRegistrationKey);
        return request;
    }

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