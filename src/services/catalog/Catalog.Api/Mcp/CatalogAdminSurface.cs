namespace Catalog.Api.Mcp;

// Catalog admin scope demeti (okuma + yazma) — auth kaydı (Program.cs) + RFC 9728 PRM keşfi için.
// (085 tool-görünürlük budaması kaldırıldı; TOOL_NAMES/TOOL_SCOPE_MAP söküldü — yetki artık yalnız
// handler'daki [RequiredScope].)
public static class CatalogAdminSurface
{
    public static readonly string[] SCOPES =
    [
        AuthorizationScopes.AdminCatalogRead,
        AuthorizationScopes.AdminCatalogWrite,
    ];
}