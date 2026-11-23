namespace ScoutAgent.Api.Agents;

// Sayfalama niyeti: kullanıcının "devamını getir / sonraki / daha fazla" türü isteğini saf kuralla
// yakalar (İlke VI test-first). Backend bunu görünce son aramanın page+1'ini çağırır (LLM-atla) →
// filtre drift'i biter. İfade-yakalama TEK yerde (React'te regex dağıtılmaz).
public static class PaginationIntent
{
    // Normalize edilmiş mesajda ARANAN parçalar (Türkçe-fold sonrası, hepsi küçük + noktasız i).
    private static readonly string[] Phrases =
        ["devamini getir", "devamini", "devami", "sonraki", "diger sonuc", "daha fazla", "devam et"];

    public static bool IsMore(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var n = Fold(message);
        foreach (var p in Phrases)
            if (n.Contains(p, StringComparison.Ordinal)) return true;
        return false;
    }

    // Türkçe-duyarlı sadeleştirme: İ/I/ı → i, ı'sız küçük harf (InvariantGlobalization açık ama girdi
    // çeşidi için açık fold daha sağlam). Diğer karakterler lower-invariant.
    private static string Fold(string s)
    {
        var chars = new char[s.Length];
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            chars[i] = c switch
            {
                'İ' or 'I' or 'ı' => 'i',
                'ç' or 'Ç' => 'c',
                'ş' or 'Ş' => 's',
                'ğ' or 'Ğ' => 'g',
                'ü' or 'Ü' => 'u',
                'ö' or 'Ö' => 'o',
                _ => char.ToLowerInvariant(c),
            };
        }
        return new string(chars);
    }
}
