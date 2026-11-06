namespace Identity.Server;

// 084: eskiden ECommerce-sabit scope/client/rol kaydı burada dizilerle duruyordu; artık uygulama-nötr.
// Sabit değerler AppRegistry (config güdümlü) üzerinden gelir; burada YALNIZ uygulama-nötr platform
// sabitleri (OIDC kimlik scope'ları, dış-agent Claude callback'leri) + registry delegasyonu kalır.
public static class Config
{
    // Identity scope'ları (openid/profile/email + role taşıyıcı "roles" + offline_access) — OIDC standart, nötr.
    public static readonly string[] IdentityScopes =
        [Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.Roles, Scopes.OfflineAccess];

    // Platform yönetim scope'u — IdP'nin kendi yüzeyi (rol yönetimi). Uygulamaya ait değil.
    // 005: apikeys.manage + dış AI callback sabitleri KIRPILDI (ApiKey/DCR yüzeyi yok).
    public const string IdentityRolesManageScope = "identity.roles.manage";

    // --- Registry delegasyonu (kayıtlı uygulamaların birleşimi; tek kaynak = AppRegistry.Current) ---

    // Tüm API scope'ları (kayıtlı app scope'ları + platform yönetim scope'ları) — seed edilir.
    public static IEnumerable<string> AllApiScopes => AppRegistry.Current.AllApiScopes;

    // Scope → audience (resource) haritası; token üretiminde 'aud' claim'ini besler.
    public static IReadOnlyDictionary<string, string[]> ScopeResources => AppRegistry.Current.ScopeResources;

    // Seed edilecek OAuth istemcileri.
    public static IReadOnlyList<ClientSeed> Clients => AppRegistry.Current.Clients;

    // Seed edilecek roller + scope demetleri (rol adı → scope'lar).
    public static IReadOnlyDictionary<string, string[]> RoleScopeSeed => AppRegistry.Current.RoleScopeSeed;
}

// Tek istemci seed tanımı (SeedHostedService OpenIddict descriptor'a çevirir).
public sealed class ClientSeed
{
    public required string ClientId { get; init; }
    // 070: public (PKCE) istemcide secret YOK — null bırakılır.
    public required string? ClientSecret { get; init; }
    public required string DisplayName { get; init; }
    // 070: public istemci (secret'sız + PKCE zorunlu); confidential seed'ler için false kalır.
    public bool IsPublic { get; init; }
    public bool AllowAuthorizationCode { get; init; }
    public bool AllowClientCredentials { get; init; }
    public bool AllowRefreshToken { get; init; }
    public string[] RedirectUris { get; init; } = [];
    public string[] PostLogoutRedirectUris { get; init; } = [];
    public string[] Scopes { get; init; } = [];
}