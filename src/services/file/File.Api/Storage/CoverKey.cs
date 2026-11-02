namespace FileApi.Storage;

// Saf: ISBN → güvenli dosya adı anahtarı. Path-traversal / ayraç / boşluk reddi.
// Allowlist (harf/rakam/tire) tek başına '/', '\', '..', boşluk dahil güvensizi eler;
// açık kontroller okunurluk için (İLKE VI test-first). Uzantısız key (spec).
public static class CoverKey
{
    public static bool TryCreate(string? isbn, out string key)
    {
        key = string.Empty;
        if (string.IsNullOrWhiteSpace(isbn)) return false;
        if (isbn.Any(char.IsWhiteSpace)) return false;          // boşluk reddi
        if (isbn.Contains('/') || isbn.Contains('\\')) return false; // ayraç reddi
        if (isbn.Contains("..")) return false;                   // traversal reddi
        if (!isbn.All(c => char.IsLetterOrDigit(c) || c == '-')) return false; // allowlist

        key = isbn;
        return true;
    }
}
