using Microsoft.Extensions.Primitives;

namespace Identity.Server.Connect;

// /connect/authorize — code+PKCE akışının kullanıcı etkileşim ucu.
public static class AuthorizeEndpoint
{
    public static void MapAuthorizeEndpoint(this WebApplication app) =>
        app.MapMethods("/connect/authorize", ["GET", "POST"], HandleAsync);

    // FLOW.md Süreç 1+4-6 — agent'ın tarayıcı açtırdığı İLK durak. Cookie yok → login'e
    // Challenge (Süreç 3'e gidiş); cookie var → kimlik claim'leri (4), rol→demet (5),
    // kesişim (6); Explicit (DCR) istemcide consent dalı. Parola bu uca HİÇ uğramaz.
    private static async Task<IResult> HandleAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictScopeManager scopeManager,
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictAuthorizationManager authorizationManager,
        RoleScopeQuery roleScopeQuery)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OIDC authorize isteği çözülemedi.");

        // WebApp "Kayıt ol" akışı prompt=create gönderir → kayıt sayfasına yönlendir.
        // create prompt'u returnUrl'den temizlenir; kayıt+login sonrası authorize tekrar
        // buraya gelince prompt=create olmadığından token akışı tamamlanır (döngü yok).
        if (request.HasPromptValue(PromptValues.Create))
        {
            var cleaned = BuildReturnUrlWithoutCreatePrompt(context);
            return Results.Redirect($"/Account/Create/Index?returnUrl={Uri.EscapeDataString(cleaned)}");
        }

        // Cookie ile kimlik doğrula. Yoksa (veya prompt=login) login'e yönlendir.
        var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var promptLogin = request.HasPromptValue(PromptValues.Login);

        if (result is not { Succeeded: true } || promptLogin)
        {
            if (promptLogin)
                await signInManager.SignOutAsync();

            var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl },
                [IdentityConstants.ApplicationScheme]);
        }

        var user = await userManager.GetUserAsync(result.Principal!)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

        identity.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user));

        // Duende AspNetIdentity paritesi: name/email kullanıcının saklı claim'lerinden (Create'te eklenir),
        // yoksa UserName/Email'e düşülür. Rol atanmaz (FR-011).
        var userClaims = await userManager.GetClaimsAsync(user);
        var name = userClaims.FirstOrDefault(c => c.Type == Claims.Name)?.Value
                   ?? await userManager.GetUserNameAsync(user);
        var email = userClaims.FirstOrDefault(c => c.Type == Claims.Email)?.Value
                    ?? await userManager.GetEmailAsync(user);
        identity.SetClaim(Claims.Name, name);
        identity.SetClaim(Claims.Email, email);

        // 030 RBAC: kullanıcının (tek) rolü id_token'a biner (WebApp header link gibi UI kararı için);
        // access token'a GİRMEZ (destinations). Yetki kararı yine scope iledir, rol değil.
        var roleName = (await userManager.GetRolesAsync(user)).FirstOrDefault();
        if (roleName is not null)
            identity.SetClaim(Claims.Role, roleName);

        // 061: Explicit consent'li istemciler (DCR dış agent'ları) kullanıcı onayı ister (FR-005).
        // Seed istemciler Implicit — bu blok onlara hiç dokunmaz.
        var application = await applicationManager.FindByClientIdAsync(request.ClientId!, context.RequestAborted)
            ?? throw new InvalidOperationException("İstemci kaydı bulunamadı.");

        // 030 RBAC + 085 R3: granted API scope'ları = requested ∩ (kimlik scope'ları ∪
        // (istemci-tavanı ∩ rol demeti)). Rol yalnız burada scope'a AÇILIR; token'a rol claim'i
        // yazılmaz → downstream yalnız scope görür. Tavan-üstü talep sessizce elenir (FR-006).
        var roleBundle = await roleScopeQuery.GetUserScopeBundleAsync(user, context.RequestAborted);
        var clientCeiling = await ClientCeilingResolver.ResolveAsync(
            applicationManager, application, context.RequestAborted);
        identity.SetScopes(ScopeResolver.Resolve(
            request.GetScopes(), roleBundle, clientCeiling, Config.IdentityScopes.ToHashSet()));

        var resources = new List<string>();
        await foreach (var resource in scopeManager.ListResourcesAsync(identity.GetScopes()))
            resources.Add(resource);
        identity.SetResources(resources);

        // 005: consent akışı KIRPILDI — tüm istemciler seed/Implicit (DCR yok, Explicit istemci
        // doğamaz). Yine de Explicit görülürse kapı kapalı: access_denied (fail-closed).
        if (await applicationManager.GetConsentTypeAsync(application, context.RequestAborted) == ConsentTypes.Explicit)
            return Results.Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.AccessDenied,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "Explicit consent istemcisi desteklenmiyor.",
                }),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

        identity.SetDestinations(OidcClaimDestinations.GetDestinations);

        return Results.SignIn(new ClaimsPrincipal(identity), null,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // Authorize URL'ini yeniden kur ama prompt değerinden "create"i çıkar.
    private static string BuildReturnUrlWithoutCreatePrompt(HttpContext context)
    {
        var query = QueryHelpers.ParseQuery(context.Request.QueryString.Value);
        var rebuilt = new Dictionary<string, StringValues>(query);

        if (rebuilt.TryGetValue(Parameters.Prompt, out var prompt))
        {
            var remaining = prompt.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p != PromptValues.Create)
                .ToArray();

            if (remaining.Length > 0)
                rebuilt[Parameters.Prompt] = string.Join(' ', remaining);
            else
                rebuilt.Remove(Parameters.Prompt);
        }

        var qs = QueryString.Create(
            rebuilt.SelectMany(kv => kv.Value.Select(v => KeyValuePair.Create(kv.Key, v))));

        return context.Request.PathBase + context.Request.Path + qs;
    }
}