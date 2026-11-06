using System.ComponentModel.DataAnnotations;

namespace Identity.Server.Options;

// 084: Platform kimlik makamının uygulama-kayıt sözleşmesi (config güdümlü). Hangi uygulamalar
// makama güvenir, her birinin scope ad-uzayı + OAuth client demetleri + RBAC rol demetleri.
// Bağlama: AddOptions<AppRegistryOptions>().BindConfiguration("AppRegistry").ValidateOnStart().
// IdP artık tek uygulamanın (ECommerce) scope setini VARSAYMAZ; ECommerce "app #1" olarak burada durur.
public sealed class AppRegistryOptions
{
    [Required, MinLength(1)]
    public List<RegisteredApp> Apps { get; set; } = [];
}

// Platform makamına güvenen bir uygulama (relying party). ECommerce ilk örnek.
public sealed class RegisteredApp
{
    // Tekil uygulama kimliği (ör. "ecommerce", "pg"). İki app aynı AppId'yi alamaz.
    public required string AppId { get; set; }

    // Bu app'in sahip olduğu yetki kümesi (scope ad-uzayı).
    public ScopeNamespaceOptions ScopeNamespace { get; set; } = new();

    // Bu app için seed edilecek OAuth client'lar.
    public List<ClientBundleOptions> Clients { get; set; } = [];

    // Bu app'in katkıda bulunduğu RBAC rol demetleri (rol adı → scope'lar). Aynı rol adına
    // birden çok app katkı verebilir (platform rolü); seed additive birleştirir.
    public List<RoleBundleOptions> Roles { get; set; } = [];
}

public sealed class ScopeNamespaceOptions
{
    // App'in üst-prefix'i (yeni app'ler için "pg", "x"). ECommerce = mevcut BC scope kümesi (toplu).
    public string Prefix { get; set; } = "";

    public List<ScopeEntry> Scopes { get; set; } = [];
}

// Bir scope atomu: adı + API audience eşlemesi (+ insan-okur açıklama, admin ekranı için).
public sealed class ScopeEntry
{
    public required string Name { get; set; }
    // Token üretiminde 'aud' claim'ini besleyen resource; null ise resource'suz (ör. identity yönetim).
    public string? Audience { get; set; }
    // 088: çapraz-kesen scope (ör. ops.deadletter) birden çok servis audience'ına çözülür.
    // Doluysa Audience yerine bu kullanılır → token aud'u tüm listeyi taşır (çok-servis erişim).
    public List<string>? Audiences { get; set; }
    public string? Description { get; set; }
}

// Bir OAuth client seed'i (Config.ClientSeed'in config karşılığı).
public sealed class ClientBundleOptions
{
    public required string ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public required string DisplayName { get; set; }
    public bool IsPublic { get; set; }
    public bool AllowAuthorizationCode { get; set; }
    public bool AllowClientCredentials { get; set; }
    public bool AllowRefreshToken { get; set; }
    public List<string> RedirectUris { get; set; } = [];
    // 006: çıkışta (end-session) dönülecek adresler — SPA logout'u IdP oturumunu da kapatıp buraya döner.
    public List<string> PostLogoutRedirectUris { get; set; } = [];
    public List<string> Scopes { get; set; } = [];
    // 070/073: RFC 8252 §7.3 loopback (dinamik port) redirect muafiyeti YALNIZ işaretli client'lara.
    public bool AllowLoopbackRedirect { get; set; }
    // 061: DCR (dış agent) kaydının varsayılan grant-edilebilir scope demetini bu client'ın api
    // scope'ları belirler (tek app'te tek işaretli client). Yoksa DCR api scope demeti boştur.
    public bool DcrTemplate { get; set; }
}

public sealed class RoleBundleOptions
{
    public required string Name { get; set; }
    public List<string> Scopes { get; set; } = [];
}