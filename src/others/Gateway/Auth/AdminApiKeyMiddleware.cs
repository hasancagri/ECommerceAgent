using Gateway.Options;

namespace Gateway.Auth;

// Admin rota sınıfının kenar kapısı: /api/admin/** altına giden her istek X-Admin-Api-Key ister.
// Eşleşmeyen istek 401 ile BURADA ölür — proxy'ye (hedef servise) hiç düşmez (FR-013).
// Karşılaştırma sabit-zamanlı (timing saldırısına karşı); anahtar değeri loglanmaz (İlke VIII).
// GEÇİCİ Dilim-1 düzeni — Dilim 2'de JWT/scope doğrulaması bu middleware'in yerini alır.
public class AdminApiKeyMiddleware(RequestDelegate next, GatewayAuthOptions options)
{
    public const string HeaderName = "X-Admin-Api-Key";
    private const string AdminPathPrefix = "/api/admin";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(AdminPathPrefix))
        {
            await next(context);
            return;
        }

        var provided = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(provided) || !SecretCompare.FixedTimeEquals(provided, options.AdminApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }
}
