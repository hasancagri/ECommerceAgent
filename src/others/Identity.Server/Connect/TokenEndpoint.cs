namespace Identity.Server.Connect;

// /connect/token — client_credentials + authorization_code(+PKCE) + refresh_token exchange.
public static class TokenEndpoint
{
    public static void MapTokenEndpoint(this WebApplication app) =>
        app.MapPost("/connect/token", HandleAsync);

    // FLOW.md Süreç 7-9 — arka kanal (kullanıcı ekranı yok): code→token takası (7; aud
    // scope'lardan üretilir), refresh'te GÜNCEL rol demetiyle yeniden süzme (8; rol düştüyse
    // yetki daralır), m2m'de sub=client id + rol yok (9).
    private static async Task<IResult> HandleAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictScopeManager scopeManager,
        IOpenIddictApplicationManager applicationManager,
        RoleScopeQuery roleScopeQuery)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OIDC token isteği çözülemedi.");

        // M2M: sub = client id (servisler CurrentUser.Load ile okur). apikeys.admin + order-saga.
        if (request.IsClientCredentialsGrantType())
        {
            var identity = new ClaimsIdentity(
                TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

            identity.SetClaim(Claims.Subject, request.ClientId);

            identity.SetScopes(request.GetScopes());

            var resources = new List<string>();
            await foreach (var resource in scopeManager.ListResourcesAsync(identity.GetScopes()))
                resources.Add(resource);
            identity.SetResources(resources);

            identity.SetDestinations(OidcClaimDestinations.GetDestinations);

            return Results.SignIn(new ClaimsPrincipal(identity), null,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // Kullanıcı akışları: OpenIddict'in code/refresh token'da sakladığı principal'ı geri al.
        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            // Süresi geçmiş/iptal edilmiş code veya refresh token: OpenIddict authentication'ı
            // Principal ÜRETMEDEN başarısız döner. Kontrolsüz dereference önce 500 fırlatıyordu;
            // istemci (mcp-remote) invalid_grant bekler → tam re-authorization'a bu şekilde düşer.
            if (!result.Succeeded || result.Principal is not { } principal)
            {
                return Results.Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "Token süresi geçmiş veya iptal edilmiş.",
                    }),
                    [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            // Kullanıcı hâlâ giriş yapabiliyor mu (silinmiş/kilitli değil).
            var user = await userManager.FindByIdAsync(principal.GetClaim(Claims.Subject)!);
            if (user is null || !await signInManager.CanSignInAsync(user))
            {
                return Results.Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "Kullanıcı artık giriş yapamıyor.",
                    }),
                    [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            // Destinasyonlar yeniden hesaplanır (refresh'te de claim'ler doğru token'a gitsin).
            var identity = new ClaimsIdentity(principal.Claims,
                TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

            // 030 RBAC: refresh'te scope'ları kullanıcının GÜNCEL rol demetiyle yeniden süz (FR-012) —
            // rol düşürülmüşse yeni yetki bir sonraki (refresh) token'da daralır. Rol yükseltme
            // authorize'da geniş talep zaten süzüldüğünden re-login gerektirir. Kaynaklar da güncellenir.
            if (request.IsRefreshTokenGrantType())
            {
                var roleBundle = await roleScopeQuery.GetUserScopeBundleAsync(user, context.RequestAborted);
                var application = await applicationManager.FindByClientIdAsync(request.ClientId!, context.RequestAborted)
                    ?? throw new InvalidOperationException("İstemci kaydı bulunamadı.");
                var clientCeiling = await ClientCeilingResolver.ResolveAsync(
                    applicationManager, application, context.RequestAborted);
                identity.SetScopes(ScopeResolver.Resolve(
                    identity.GetScopes(), roleBundle, clientCeiling, Config.IdentityScopes.ToHashSet()));

                var resources = new List<string>();
                await foreach (var resource in scopeManager.ListResourcesAsync(identity.GetScopes()))
                    resources.Add(resource);
                identity.SetResources(resources);
            }

            identity.SetDestinations(OidcClaimDestinations.GetDestinations);

            return Results.SignIn(new ClaimsPrincipal(identity), null,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException("Desteklenmeyen grant type.");
    }
}