using System.Text;

namespace Personalization.Api.Domains.SearchHistory;

// 013: türetilmiş geçmiş görünümü — kalıcı değil, okuma anında ham UserSearchRecord'lardan hesaplanır.
public record SearchHistoryEntry(
    string Query,            // grubun en son kaydının ham yazımı (gösterim)
    string NormalizedQuery,  // dedup anahtarı — API dışına sızmaz
    string Mode,             // grubun en son modu (tekrar-ara bununla)
    DateTimeOffset LastSearchedAt,
    int Count);

// 013: saf tekilleştirme + Türkçe-duyarlı normalize (İlke VI test-first). Ham kayıt listesini
// benzersiz-sorgu görünümüne indirger; SQL değil in-memory (kullanıcı başına kayıt az + Türkçe-fold).
public static class SearchHistoryProjection
{
    // NormalizedQuery eşitliğine göre grupla; her grupta en son (SearchedAt max) kaydın yazımı+modu;
    // LastSearchedAt azalan sıralı.
    public static IReadOnlyList<SearchHistoryEntry> Deduplicate(IEnumerable<UserSearchRecord> records)
        => records
            .GroupBy(r => Normalize(r.Query))
            .Select(g =>
            {
                var latest = g.OrderByDescending(r => r.SearchedAt).First();
                return new SearchHistoryEntry(
                    Query: latest.Query,
                    NormalizedQuery: g.Key,
                    Mode: latest.Mode,
                    LastSearchedAt: latest.SearchedAt,
                    Count: g.Count());
            })
            .OrderByDescending(e => e.LastSearchedAt)
            .ToList();

    // Türkçe-duyarlı dedup anahtarı: baş/son trim + iç boşluk sadeleştir + i-ailesi (İ/I/ı/i) → 'i'
    // + diğer harfler ToLowerInvariant. i-ailesi fold ÖNCE yapılır (InvariantGlobalization'da 'İ'
    // ToLowerInvariant birleşik-işaret üretir — bu tuzaktan kaçınılır).
    public static string Normalize(string query)
    {
        var collapsed = string.Join(' ', (query ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        var sb = new StringBuilder(collapsed.Length);
        foreach (var ch in collapsed)
            sb.Append(ch is 'İ' or 'I' or 'ı' or 'i' ? 'i' : char.ToLowerInvariant(ch));
        return sb.ToString();
    }
}
