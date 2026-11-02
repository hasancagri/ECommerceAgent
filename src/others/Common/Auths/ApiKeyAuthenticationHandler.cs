using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;

namespace Common.Auths;

// X-User-Key → Identity.Server resolve → ClaimsPrincipal(sub/email/scope).
// Header yok → NoResult() (anonim; okumalar geçer). Geçersiz/iptalli → Fail() (401).
// CurrentUser.Load claim'leri principal'den okuduğu için servis/handler kodu değişmez.
public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHttpClientFactory httpClientFactory)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var rawValues)
            || string.IsNullOrWhiteSpace(rawValues))
            return AuthenticateResult.NoResult();

        var rawKey = rawValues.ToString();

        var client = httpClientFactory.CreateClient(ApiKeyAuthenticationDefaults.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, Options.ResolvePath);
        if (!string.IsNullOrEmpty(Options.InternalSecret))
            request.Headers.Add("X-Internal-Secret", Options.InternalSecret);
        request.Content = JsonContent.Create(new ResolveRequest(rawKey));

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, Context.RequestAborted);
        }
        catch (Exception ex)
        {
            return AuthenticateResult.Fail(ex);
        }

        if (response.StatusCode != HttpStatusCode.OK)
            return AuthenticateResult.Fail("Invalid or revoked API key");

        var payload = await response.Content.ReadFromJsonAsync<ResolveResponse>(Context.RequestAborted);
        if (payload is null || string.IsNullOrEmpty(payload.UserId))
            return AuthenticateResult.Fail("Invalid resolve payload");

        var claims = new List<Claim> { new("sub", payload.UserId) };
        if (!string.IsNullOrEmpty(payload.Email))
            claims.Add(new Claim("email", payload.Email));
        if (!string.IsNullOrEmpty(payload.GivenName))
            claims.Add(new Claim("given_name", payload.GivenName));
        if (!string.IsNullOrEmpty(payload.FamilyName))
            claims.Add(new Claim("family_name", payload.FamilyName));
        foreach (var scope in payload.Scopes ?? [])
            claims.Add(new Claim("scope", scope));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    private record ResolveRequest(string Key);

    private record ResolveResponse(string UserId, string? Email, string? GivenName, string? FamilyName, string[]? Scopes);
}