namespace Catalog.Api.Common;

// Category/Brand teklik anahtarı üretimi (research R3). Fabrikalar ve get-or-create
// sorguları AYNI fonksiyonu kullanır ki eşleşme iki tarafta da tutarlı olsun.
// Teknik helper (domain değil) — Domains/ dışında.
public static class NameNormalization
{
    public static string Normalize(string name)
    {
        var collapsed = System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"\s+", " ");
        return collapsed.ToUpperInvariant();
    }
}
