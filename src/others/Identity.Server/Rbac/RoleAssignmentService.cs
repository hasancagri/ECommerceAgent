namespace Identity.Server.Rbac;

// 030 RBAC: admin yönetim yüzeyinin + register + seed'in kullandığı rol/scope/atama servisi.
// Saf kurallar (RoleAssignmentRules, AssignableScopeValidator) burada Identity/EF ile birleşir.
public sealed class RoleAssignmentService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext db)
{
    // 005: super-admin KIRPILDI (çok-uygulama platform kavramıydı; tek-app EC'de admin yeter).
    public static readonly string[] SeedRoles = ["admin", "customer"];
    public const string AdminRole = "admin";
    public const string CustomerRole = "customer";

    public sealed record OperationResult(bool Success, string? Error)
    {
        public static OperationResult Ok() => new(true, null);
        public static OperationResult Fail(string error) => new(false, error);
    }

    // --- Roller ---

    public Task<List<IdentityRole>> ListRolesAsync() => roleManager.Roles.ToListAsync();

    public async Task<OperationResult> CreateRoleAsync(string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Fail("Rol adı boş olamaz.");
        if (await roleManager.RoleExistsAsync(name))
            return OperationResult.Fail("Bu adda bir rol zaten var.");

        var result = await roleManager.CreateAsync(new IdentityRole(name));
        return result.Succeeded
            ? OperationResult.Ok()
            : OperationResult.Fail(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public async Task<OperationResult> DeleteRoleAsync(string roleId)
    {
        var role = await roleManager.FindByIdAsync(roleId);
        if (role is null)
            return OperationResult.Fail("Rol bulunamadı.");
        if (IsSeedRole(role.Name))
            return OperationResult.Fail("Seed rolü (admin/customer) silinemez.");

        var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Count > 0)
            return OperationResult.Fail("Bu role atanmış kullanıcılar var; önce başka role taşıyın.");

        // Rol→scope satırlarını da temizle.
        var links = await db.RoleScopes.Where(rs => rs.RoleId == role.Id).ToListAsync();
        db.RoleScopes.RemoveRange(links);
        await db.SaveChangesAsync();

        var result = await roleManager.DeleteAsync(role);
        return result.Succeeded
            ? OperationResult.Ok()
            : OperationResult.Fail(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    // --- Rol → Scope ---

    public async Task<IReadOnlyList<string>> GetRoleScopesAsync(string roleId) =>
        await db.RoleScopes.Where(rs => rs.RoleId == roleId).Select(rs => rs.Scope).ToListAsync();

    // Rolün scope kümesini verilen listeye SET eder. Tüm scope'lar KnownScopes'ta olmalı (INV-1).
    public async Task<OperationResult> SetRoleScopesAsync(string roleId, IReadOnlyList<string> scopes)
    {
        var role = await roleManager.FindByIdAsync(roleId);
        if (role is null)
            return OperationResult.Fail("Rol bulunamadı.");

        var distinct = scopes.Distinct().ToList();
        var unknown = AssignableScopeValidator.FindUnknown(distinct, KnownScopes.Names);
        if (unknown.Count > 0)
            return OperationResult.Fail($"Bilinmeyen scope: {string.Join(", ", unknown)}");

        var existing = await db.RoleScopes.Where(rs => rs.RoleId == roleId).ToListAsync();
        db.RoleScopes.RemoveRange(existing);
        foreach (var scope in distinct)
            db.RoleScopes.Add(new RoleScope { Id = Guid.NewGuid(), RoleId = roleId, Scope = scope });

        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    // --- Kullanıcı → Rol (tek rol) ---

    public async Task<string?> GetUserRoleAsync(ApplicationUser user) =>
        (await userManager.GetRolesAsync(user)).FirstOrDefault();

    // Kullanıcının rolünü hedefe çevirir (tek-rol). Son admin'in rolü değiştirilemez (INV-4).
    public async Task<OperationResult> SetUserRoleAsync(ApplicationUser user, string targetRole)
    {
        if (!await roleManager.RoleExistsAsync(targetRole))
            return OperationResult.Fail("Hedef rol bulunamadı.");

        var current = await userManager.GetRolesAsync(user);

        // Son admin koruması: kullanıcı admin'den çıkarılıyorsa ve başka admin yoksa reddet.
        var losingAdmin = current.Any(r => string.Equals(r, AdminRole, StringComparison.OrdinalIgnoreCase))
                          && !string.Equals(targetRole, AdminRole, StringComparison.OrdinalIgnoreCase);
        if (losingAdmin)
        {
            var admins = await userManager.GetUsersInRoleAsync(AdminRole);
            if (admins.Count <= 1)
                return OperationResult.Fail("Sistemdeki son admin'in rolü değiştirilemez.");
        }

        var plan = RoleAssignmentRules.ApplySingleRole(current, targetRole);
        if (plan.RolesToRemove.Count > 0)
            await userManager.RemoveFromRolesAsync(user, plan.RolesToRemove);
        if (plan.RoleToAdd is not null)
            await userManager.AddToRoleAsync(user, plan.RoleToAdd);

        return OperationResult.Ok();
    }

    public static bool IsSeedRole(string? name) =>
        name is not null && SeedRoles.Any(r => string.Equals(r, name, StringComparison.OrdinalIgnoreCase));
}