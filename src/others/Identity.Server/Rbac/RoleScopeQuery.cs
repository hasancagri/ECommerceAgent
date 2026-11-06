namespace Identity.Server.Rbac;

// 030 RBAC: bir kullanıcının rol demetini (rol→scope) çözer. Token verme yolunda
// (AuthorizeEndpoint/TokenEndpoint) ScopeResolver'a beslenir. Tek-rol beklenir ama
// savunmacı olarak birden çok rolün scope'ları birleştirilir.
public sealed class RoleScopeQuery(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext db)
{
    // FLOW.md Süreç 5 — rolün scope demetine AÇILDIĞI yer. Customer'ın admin-istemciyle
    // girme denemesi burada söner: demette yönetim scope'u yok → kesişim yalnız kimlik scope'ları bırakır.
    public async Task<ISet<string>> GetUserScopeBundleAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var roleNames = await userManager.GetRolesAsync(user);
        if (roleNames.Count == 0)
            return new HashSet<string>();

        var roleIds = new List<string>();
        foreach (var name in roleNames)
        {
            var role = await roleManager.FindByNameAsync(name);
            if (role is not null)
                roleIds.Add(role.Id);
        }

        if (roleIds.Count == 0)
            return new HashSet<string>();

        var scopes = await db.RoleScopes
            .Where(rs => roleIds.Contains(rs.RoleId))
            .Select(rs => rs.Scope)
            .ToListAsync(ct);

        return scopes.ToHashSet();
    }
}