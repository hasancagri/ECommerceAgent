namespace Common.Auths;

// X-User-Key varsa ApiKey şemasını EAGER çözer: geçersiz → 401 (anonim okumada bile, D5/FR-009);
// geçerli → principal'i Context.User'a koyar (okumalar da kişiselleşir, yazma yetkisi kurulur).
// UseAuthentication'dan SONRA, UseAuthorization'dan ÖNCE çağrılmalı.
public class ApiKeyAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.ContainsKey(ApiKeyAuthenticationOptions.HeaderName))
        {
            var result = await context.AuthenticateAsync(ApiKeyAuthenticationDefaults.Scheme);
            if (!result.Succeeded)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            context.User = result.Principal!;
        }

        await next(context);
    }
}