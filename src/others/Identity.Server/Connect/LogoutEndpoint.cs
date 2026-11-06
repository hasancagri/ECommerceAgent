namespace Identity.Server.Connect;

// /connect/logout — end-session ucu. WebApp OIDC signout'u buraya gelir; IdP cookie'si düşer,
// OpenIddict post_logout_redirect_uri'yi doğrular ve oraya (WebApp'e) döner.
public static class LogoutEndpoint
{
    public static void MapLogoutEndpoint(this WebApplication app) =>
        app.MapMethods("/connect/logout", ["GET", "POST"], HandleAsync);

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();

        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }
}