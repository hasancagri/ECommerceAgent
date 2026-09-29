using System.Net.Http.Headers;

namespace Customer.Api.Onboarding;

// (032'den taşındı): DropShop Merchant.Api /mcp yüzeyi merchant.write ister; Customer.Api oraya
// MAKİNE kimliğiyle bağlanır (client_credentials, ecommerce-onboarding). Admin kullanıcı token'ı
// gateway'e taşınmaz. Token DropShop Identity connect/token'dan alınır, süresi dolana dek (−30 sn
// güvenlik payı) cache'lenir. Handler her isteğe Bearer takar. DropShop dev cert self-signed →
// bu istemcide sertifika doğrulaması kapalı (dev).
public sealed class OnboardingGatewayTokenHandler(DropShopOnboardingOption gateway) : DelegatingHandler
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static string? _token;
    private static DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    private static readonly HttpClient TokenHttp = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _token;

        await Gate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _token;

            using var resp = await TokenHttp.PostAsync(gateway.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = gateway.ClientId,
                    ["client_secret"] = gateway.ClientSecret,
                    ["scope"] = gateway.Scope
                }), ct);
            resp.EnsureSuccessStatusCode();

            using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = json.RootElement;
            _token = root.GetProperty("access_token").GetString()!;
            var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 300;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 30);
            return _token;
        }
        finally
        {
            Gate.Release();
        }
    }
}