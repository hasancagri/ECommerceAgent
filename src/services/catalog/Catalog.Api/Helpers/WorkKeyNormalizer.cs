using System.Text.RegularExpressions;

namespace Catalog.Api.Helpers;

// 021: Eser kimliği (WorkKey) üretimi — aynı eserin farklı baskıları (ISBN/cilt/çevirmen farkı) TEK
// anahtara iner; benzerlik (similar_books) kendi baskılarını tekilleştirir. İç alandır (kullanıcıya
// çıkmaz). Teknik helper (domain değil) — Domains/ dışında, NameNormalization komşusu.
public static partial class WorkKeyNormalizer
{
    /// <summary>normalize(sıralı+tekil yazarlar) + "|" + normalize(parantezsiz başlık).
    /// normalize: Türkçe fold → ASCII, lowercase invariant, çoklu boşluk tek. Yazarsız → "|baslik".</summary>
    public static string Derive(IEnumerable<string> authorNames, string title)
    {
        var authors = authorNames
            .Select(Normalize)
            .Where(a => a.Length > 0)
            .Distinct()
            .OrderBy(a => a, StringComparer.Ordinal)
            .ToList();

        var normalizedTitle = Normalize(StripParentheses(title));
        return string.Join(",", authors) + "|" + normalizedTitle;
    }

    // Parantez içi ekleri atar ("(Ciltli)", "(Özel Baskı)") — baskı varyantı eser kimliğine girmez.
    private static string StripParentheses(string value) => ParenthesesRegex().Replace(value, " ");

    private static string Normalize(string value)
    {
        var folded = FoldTurkish(value);
        var collapsed = WhitespaceRegex().Replace(folded.Trim(), " ");
        return collapsed.ToLowerInvariant();
    }

    // Türkçe karakterleri ASCII'ye katlar (iki durum da) — InvariantGlobalization'ın İ/ı belirsizliğini
    // önler (lowercase ÖNCESİ sabit eşleme).
    private static string FoldTurkish(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
            sb.Append(ch switch
            {
                'ç' or 'Ç' => 'c',
                'ğ' or 'Ğ' => 'g',
                'ı' or 'I' => 'i',
                'İ' or 'i' => 'i',
                'ö' or 'Ö' => 'o',
                'ş' or 'Ş' => 's',
                'ü' or 'Ü' => 'u',
                'â' or 'Â' => 'a',
                'î' or 'Î' => 'i',
                'û' or 'Û' => 'u',
                _ => ch,
            });
        return sb.ToString();
    }

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex ParenthesesRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
