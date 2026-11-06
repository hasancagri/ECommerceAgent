namespace Identity.Server.Rbac;

// 085 R3: istemci-tavanı = uygulama kaydının izinli scope Permissions'ı (Permissions.Prefixes.Scope
// öneki). ScopeResolver bu tavanı 3. duvar olarak kullanır; tek kaynak burası (seed + DCR aynı okur).
public static class ClientCeilingResolver
{
    public static async Task<ISet<string>> ResolveAsync(
        IOpenIddictApplicationManager applicationManager, object application, CancellationToken ct)
    {
        var permissions = await applicationManager.GetPermissionsAsync(application, ct);
        return permissions
            .Where(p => p.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal))
            .Select(p => p[Permissions.Prefixes.Scope.Length..])
            .ToHashSet();
    }
}
