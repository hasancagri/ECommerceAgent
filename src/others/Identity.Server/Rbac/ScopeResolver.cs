namespace Identity.Server.Rbac;

// 030 RBAC + 085 R3 (saf): token verme anında granted API scope'ları =
// requested ∩ (her-zaman-izinli ∪ (istemci-tavanı ∩ rol demeti)). Kimlik scope'ları
// (openid/profile/email/roles/offline_access) hem tavan hem rol duvarından bağımsız her zaman
// geçer; API scope'u hem istemcinin izinli demetinde hem kullanıcının rol demetinde OLMALI (INV-6).
public static class ScopeResolver
{
    // FLOW.md Süreç 6 — akışın kalbi. Üç duvar da (istemci tavanı, rol demeti, her-zaman-izinli)
    // burada kesişir. Tavan-üstü/rol-dışı talep HATA DEĞİL — sessizce elenir (FR-006): union PRM'de
    // istemci kendi tavanının dışında scope talep edebilir, bağlantı kırılmaz.
    public static IReadOnlyList<string> Resolve(
        IEnumerable<string> requested,
        ISet<string> roleBundle,
        ISet<string> clientCeiling,
        ISet<string> alwaysAllow) =>
        [.. requested
            .Where(s => alwaysAllow.Contains(s) || (clientCeiling.Contains(s) && roleBundle.Contains(s)))
            .Distinct()];
}