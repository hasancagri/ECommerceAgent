namespace Identity.Server.Connect;

// /connect/userinfo — WebApp GetClaimsFromUserInfoEndpoint=true olduğundan name/email(/role) döner.
public static class UserInfoEndpoint
{
    public static void MapUserInfoEndpoint(this WebApplication app) =>
        app.MapMethods("/connect/userinfo", ["GET", "POST"], HandleAsync);

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager)
    {
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var subject = result.Principal?.GetClaim(Claims.Subject);
        if (string.IsNullOrEmpty(subject))
            return Results.Unauthorized();

        var user = await userManager.FindByIdAsync(subject);
        if (user is null)
            return Results.Unauthorized();

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = subject,
        };

        var userClaims = await userManager.GetClaimsAsync(user);
        var name = userClaims.FirstOrDefault(c => c.Type == Claims.Name)?.Value
                   ?? await userManager.GetUserNameAsync(user);
        var email = userClaims.FirstOrDefault(c => c.Type == Claims.Email)?.Value
                    ?? await userManager.GetEmailAsync(user);

        if (!string.IsNullOrEmpty(name)) claims[Claims.Name] = name;
        if (!string.IsNullOrEmpty(email)) claims[Claims.Email] = email;

        // 030 RBAC: kullanıcının (tek) rolü userinfo'da da döner — WebApp GetClaimsFromUserInfoEndpoint=true
        // olduğundan claim kaynağı burasıdır; UI (admin link) için gerekli. Downstream yalnız scope zorlar.
        var role = (await userManager.GetRolesAsync(user)).FirstOrDefault();
        if (!string.IsNullOrEmpty(role)) claims[Claims.Role] = role;

        return Results.Json(claims);
    }
}