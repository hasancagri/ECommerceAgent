namespace Identity.Server.Rbac;

// 030 RBAC (saf): rol→scope yazımında KnownScopes kapalılığını zorlar (INV-1). Serbest metin
// / typo / uydurma scope reddedilir — uyumsuzluk giriş anında engellenir.
public static class AssignableScopeValidator
{
    public static IReadOnlyList<string> FindUnknown(IEnumerable<string> scopes, ISet<string> known) =>
        [.. scopes.Where(s => !known.Contains(s)).Distinct()];

    public static bool AllKnown(IEnumerable<string> scopes, ISet<string> known) =>
        FindUnknown(scopes, known).Count == 0;
}