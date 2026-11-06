using Identity.Server.Options;

namespace Identity.Server.Rbac;

// 030 RBAC: atanabilir scope'ların KAPALI listesi. 084 sonrası tek kaynak = AppRegistry (config
// güdümlü kayıtlı uygulamaların scope birleşimi + platform yönetim scope'ları). Admin yönetim
// ekranının checkbox kaynağı + rol→scope yazımının doğrulayıcısı. DB/ekran yeni scope ÜRETEMEZ.
public static class KnownScopes
{
    public sealed record ScopeInfo(string Name, string Description);

    // Atanabilir tüm scope'lar + açıklama (AppRegistry.Current tek kaynak).
    public static IReadOnlyList<ScopeInfo> All =>
        [.. AppRegistry.Current.KnownScopeInfos.Select(s => new ScopeInfo(s.Name, s.Description))];

    // Hızlı üyelik kontrolü (doğrulama için).
    public static ISet<string> Names => AppRegistry.Current.AllApiScopes.ToHashSet();

    public static bool IsKnown(string scope) => Names.Contains(scope);
}
