namespace Identity.Server.Options;

// 084: app-kayıt sözleşmesinin SAF doğrulaması (İlke VI test-first birimi). Kurallar:
// specs/084-platform-idp/contracts/app-registration.md. Boot bu geçmezse açılışı REDDEDER (SC-004).
public static class AppRegistryValidator
{
    // IdP'nin KENDİ yönetim scope'ları (uygulama-nötr; her app client/rol'ü grant edebilir).
    public static readonly string[] PlatformManagementScopes = ["apikeys.manage", "identity.roles.manage"];

    // OIDC standart kimlik scope'ları (uygulamaya ait değil; her client/rol grant edebilir).
    public static readonly string[] IdentityScopes =
        ["openid", "profile", "email", "roles", "offline_access"];

    // Doğrulama hatalarını döndürür (boş liste = geçerli). Çağıran boot'ta hata varsa fırlatır.
    public static IReadOnlyList<string> Validate(AppRegistryOptions options)
    {
        var errors = new List<string>();

        if (options.Apps.Count == 0)
        {
            errors.Add("AppRegistry:Apps boş — en az bir kayıtlı uygulama gerekir.");
            return errors;
        }

        // 1. AppId tekil.
        var dupeApps = options.Apps.GroupBy(a => a.AppId, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var id in dupeApps)
            errors.Add($"AppId tekil değil: '{id}' birden çok kez tanımlı.");

        // Grant-edilebilir ek scope'lar (app scope'u olmayan ama her yerde kabul edilen).
        var neutralScopes = PlatformManagementScopes.Concat(IdentityScopes).ToHashSet(StringComparer.Ordinal);

        // 2. Scope kesişmezliği — iki farklı app aynı scope adını sahiplenemez (SC-004; FR-003).
        var scopeOwner = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var app in options.Apps)
        {
            foreach (var s in app.ScopeNamespace.Scopes)
            {
                if (neutralScopes.Contains(s.Name))
                {
                    errors.Add($"App '{app.AppId}' nötr/platform scope'unu ('{s.Name}') sahiplenemez.");
                    continue;
                }
                if (scopeOwner.TryGetValue(s.Name, out var owner) && owner != app.AppId)
                    errors.Add($"Scope ad-uzayı çakışması: '{s.Name}' hem '{owner}' hem '{app.AppId}' tarafından isteniyor.");
                else
                    scopeOwner[s.Name] = app.AppId;
            }
        }

        // 047: paylaşılan çok-app client'ları (external-admin-agent, mcp-gateway-discovery — tek dış MCP
        // yüzeyi, tek login) BİRDEN çok app'in scope'unu grant edebilir → client ceiling'i GLOBAL evrene
        // (tüm app scope'ları + nötr) göre doğrulanır; kayıtlı-olmayan (phantom) scope yine RET. Rol demeti
        // KENDİ app'ine bağlı kalır (çok-app rolü ad-birleşimiyle AppRegistry projeksiyonunda toplanır).
        var universe = scopeOwner.Keys.Concat(neutralScopes).ToHashSet(StringComparer.Ordinal);

        foreach (var app in options.Apps)
        {
            var own = app.ScopeNamespace.Scopes.Select(s => s.Name)
                .Concat(neutralScopes).ToHashSet(StringComparer.Ordinal);

            // 3. Client scope KAYITLI olmalı (herhangi bir app'in scope'u ya da nötr) — phantom RET.
            foreach (var c in app.Clients)
                foreach (var s in c.Scopes)
                    if (!universe.Contains(s))
                        errors.Add($"App '{app.AppId}' client '{c.ClientId}' kayıtlı olmayan scope grant ediyor: '{s}'.");

            // 4/5. Rol scope KENDİ app'inin (+ nötr) — yabancı app scope'u RET (ad-birleşimiyle katkı yap).
            foreach (var r in app.Roles)
                foreach (var s in r.Scopes)
                    if (!own.Contains(s))
                        errors.Add($"App '{app.AppId}' rol '{r.Name}' kendi app'ine ait olmayan scope içeriyor: '{s}'.");
        }

        return errors;
    }
}