namespace Identity.Server.Rbac;

// 030 RBAC: bir rolün (AspNetRoles) bir KnownScope'a bağlanması. Rol demetini oluşturur.
// Scope daima KnownScopes içinde olmalı (AssignableScopeValidator zorlar); (RoleId,Scope) unique.
public class RoleScope
{
    public Guid Id { get; set; }
    public string RoleId { get; set; } = default!;
    public string Scope { get; set; } = default!;
}