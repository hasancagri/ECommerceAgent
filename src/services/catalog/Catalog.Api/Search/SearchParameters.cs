namespace Catalog.Api.Search;

// 011: arama parametre validasyonu/normalizasyonu — saf (host'suz test edilebilir). Boş sorgu HATA
// değil (FR-003), limit makul üst sınırla guard'lanır (FR-004). Handler'lar arama yürütmeden önce
// bunları geçirir; boş sorguda SQL'e hiç gitmez.
public static class SearchParameters
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;
    public const int MaxQueryLength = 200; // aşırı uzun sorgu makul biçimde kesilir (edge-case).

    /// <summary>Sorgu boş/yalnız-boşluk mu — öyleyse arama yapılmaz, boş sonuç döner (hata değil).</summary>
    public static bool IsBlankQuery(string? query) => string.IsNullOrWhiteSpace(query);

    /// <summary>Sorguyu trim'ler + iç boşlukları tekler + üst uzunluğa keser. IsBlankQuery false iken çağrılır.</summary>
    public static string NormalizeQuery(string query)
    {
        var collapsed = System.Text.RegularExpressions.Regex.Replace(query.Trim(), @"\s+", " ");
        return collapsed.Length > MaxQueryLength ? collapsed[..MaxQueryLength] : collapsed;
    }

    /// <summary>limit guard: null/0/negatif → default; üst sınırı aşan → MaxLimit.</summary>
    public static int NormalizeLimit(int? limit) =>
        limit is null or <= 0 ? DefaultLimit : Math.Min(limit.Value, MaxLimit);

    /// <summary>offset guard: null/negatif → 0.</summary>
    public static int NormalizeOffset(int? offset) =>
        offset is null or < 0 ? 0 : offset.Value;
}
