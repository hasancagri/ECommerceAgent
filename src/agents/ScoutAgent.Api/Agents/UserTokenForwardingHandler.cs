using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace ScoutAgent.Api.Agents;

// 007 FR-005: agent Singleton; kullanıcı kimliği ÇAĞRI ANINDA taşınır. Gelen chat isteğinin bearer'ını
// downstream kişisel MCP çağrısına aynen iletir (makine token değil — isteğin KENDİ token'ı). Böylece
// kişisel BC ucu her istekte scope'u DOĞRULAR (gerçek güvenlik sınırı). Token yoksa header eklenmez →
// kişisel uç 401 → McpToolSource atlar → model "giriş gerekli" der.
public sealed class UserTokenForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
            request.Headers.TryAddWithoutValidation(HeaderNames.Authorization, authorization);

        return base.SendAsync(request, cancellationToken);
    }
}
