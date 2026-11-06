namespace Identity.Server.Options;

// 084: doğrulanmış AppRegistryOptions'ın PROJEKSİYONU. IdP'nin seed/token/RBAC yolları eskiden
// Config'teki sabit dizilerden okuyordu; artık burası tek kaynak. Boot'ta bir kez kurulur (Current);
// statik çağrı yerleri (Config, KnownScopes) buradan okur, DI tüketicileri singleton alır.
public sealed class AppRegistry
{
    // Platform yönetim scope'ları — IdP'nin kendi yüzeyi (uygulama-nötr). Açıklamalarıyla.
    private static readonly Dictionary<string, string> PlatformScopeDescriptions = new()
    {
        ["apikeys.manage"] = "API anahtarı yönetimi",
        ["identity.roles.manage"] = "Rol/scope/kullanıcı yönetimi",
    };

    public IReadOnlyList<string> AllApiScopes { get; }
    public IReadOnlyDictionary<string, string[]> ScopeResources { get; }
    public IReadOnlyList<ClientSeed> Clients { get; }
    public IReadOnlyDictionary<string, string[]> RoleScopeSeed { get; }
    public IReadOnlyList<(string Name, string Description)> KnownScopeInfos { get; }
    public ISet<string> LoopbackRedirectClientIds { get; }
    public IReadOnlyList<string> DcrTemplateApiScopes { get; }

    private AppRegistry(AppRegistryOptions options)
    {
        var scopeEntries = options.Apps.SelectMany(a => a.ScopeNamespace.Scopes).ToList();

        // Scope adları (app scope'ları + platform yönetim scope'ları) — tek kaynak.
        AllApiScopes =
        [
            .. scopeEntries.Select(s => s.Name),
            .. AppRegistryValidator.PlatformManagementScopes,
        ];

        // Scope → audience(ler). Audiences doluysa çok-resource (088 ops.deadletter); yoksa tek Audience.
        // Resource'suz scope (identity yönetim) haritaya girmez.
        ScopeResources = scopeEntries
            .Select(s => (s.Name, Resources: s.Audiences is { Count: > 0 }
                ? s.Audiences.ToArray()
                : !string.IsNullOrWhiteSpace(s.Audience) ? new[] { s.Audience! } : []))
            .Where(x => x.Resources.Length > 0)
            .ToDictionary(x => x.Name, x => x.Resources, StringComparer.Ordinal);

        // Client'lar → ClientSeed (tüm app'lerin birleşimi).
        Clients =
        [
            .. options.Apps.SelectMany(a => a.Clients).Select(c => new ClientSeed
            {
                ClientId = c.ClientId,
                ClientSecret = c.ClientSecret,
                DisplayName = c.DisplayName,
                IsPublic = c.IsPublic,
                AllowAuthorizationCode = c.AllowAuthorizationCode,
                AllowClientCredentials = c.AllowClientCredentials,
                AllowRefreshToken = c.AllowRefreshToken,
                RedirectUris = [.. c.RedirectUris],
                PostLogoutRedirectUris = [.. c.PostLogoutRedirectUris],
                Scopes = [.. c.Scopes],
            }),
        ];

        // Rol → scope demeti (aynı rol adına birden çok app additive katkı verir).
        RoleScopeSeed = options.Apps.SelectMany(a => a.Roles)
            .GroupBy(r => r.Name, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(r => r.Scopes).Distinct().ToArray(),
                StringComparer.Ordinal);

        // Admin ekranı için açıklamalı scope listesi (app scope açıklaması config'ten; platform sabit).
        var descriptions = scopeEntries
            .Where(s => !string.IsNullOrWhiteSpace(s.Description))
            .ToDictionary(s => s.Name, s => s.Description!, StringComparer.Ordinal);
        KnownScopeInfos =
        [
            .. AllApiScopes.Select(n => (n,
                descriptions.TryGetValue(n, out var d) ? d
                : PlatformScopeDescriptions.TryGetValue(n, out var pd) ? pd
                : n)),
        ];

        LoopbackRedirectClientIds = options.Apps.SelectMany(a => a.Clients)
            .Where(c => c.AllowLoopbackRedirect).Select(c => c.ClientId)
            .ToHashSet(StringComparer.Ordinal);

        // DCR şablonu: işaretli client'ın api scope'ları (kimlik scope'ları hariç). Tek app'te tek işaretli.
        var identity = AppRegistryValidator.IdentityScopes.ToHashSet(StringComparer.Ordinal);
        DcrTemplateApiScopes =
        [
            .. options.Apps.SelectMany(a => a.Clients).Where(c => c.DcrTemplate)
                .SelectMany(c => c.Scopes).Where(s => !identity.Contains(s)).Distinct(),
        ];
    }

    // Boot projeksiyonu — doğrulama başarısızsa fırlatır (açılış REDDEDER).
    public static AppRegistry Build(AppRegistryOptions options)
    {
        var errors = AppRegistryValidator.Validate(options);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "AppRegistry doğrulaması başarısız:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        return new AppRegistry(options);
    }

    // Statik çağrı yerleri için tek örnek (Program.cs boot'ta kurar).
    public static AppRegistry Current { get; private set; } = null!;

    public static void Initialize(AppRegistry registry) => Current = registry;
}