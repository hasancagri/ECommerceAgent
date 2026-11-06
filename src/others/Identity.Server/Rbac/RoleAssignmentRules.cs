namespace Identity.Server.Rbac;

// 030 RBAC (saf): tek-rol atama planı. Hedef dışındaki tüm roller kaldırılır; hedef yoksa
// eklenir (INV-2 tek rol, INV-3 rolsüz kalmama). Karşılaştırma büyük/küçük harf duyarsız.
public static class RoleAssignmentRules
{
    public sealed record AssignmentPlan(IReadOnlyList<string> RolesToRemove, string? RoleToAdd);

    public static AssignmentPlan ApplySingleRole(IEnumerable<string> currentRoles, string targetRole)
    {
        var current = currentRoles.ToList();

        var toRemove = current
            .Where(r => !string.Equals(r, targetRole, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var alreadyHasTarget = current
            .Any(r => string.Equals(r, targetRole, StringComparison.OrdinalIgnoreCase));

        return new AssignmentPlan(toRemove, alreadyHasTarget ? null : targetRole);
    }
}