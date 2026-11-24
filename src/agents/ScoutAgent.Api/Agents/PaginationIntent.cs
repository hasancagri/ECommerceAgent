namespace ScoutAgent.Api.Agents;

// Sayfalama niyeti: kullanıcının "devamını getir / sonraki / daha fazla" türü isteğini saf kuralla
// yakalar (İlke VI test-first). Backend bunu görünce son aramanın page+1'ini çağırır (LLM-atla) →
// filtre drift'i biter. İfade-yakalama TEK yerde (React'te regex dağıtılmaz).
//
// TUZAK: naif `Contains` YANLIŞ tetikler — "daha fazla Dostoyevski" / "sonraki baskı" / "bu kitabın
// devamı var mı" gibi GERÇEK sorgularda pagination kelimesi geçer; bypass bayat filtreyle gerçek
// sorguyu ezer (drift'i öbür yönden geri getirir). Çözüm: fold + noktalama/filler SÖK → kalan tam
// olarak bir core ifadeye eşit mi? (anchored). Böylece ifade cümlenin TAMAMI olmalı.
public static class PaginationIntent
{
    // Filler sökülünce kalması gereken TAM ifadeler (folded). Mesaj bunlardan birine indirgenmezse pagination değil.
    private static readonly HashSet<string> Core =
        ["devamini", "devami", "devam", "sonraki", "sonrakiler", "diger sonuclar", "diger sonuclari", "daha fazla"];

    // Anlam taşımayan, sökülebilir kelimeler (nezaket/fiil/edat). Sökülünce core kalır.
    private static readonly HashSet<string> Filler =
        ["getir", "getirir", "getirsene", "goster", "gostersene", "misin", "misiniz", "mi",
         "lutfen", "rica", "ederim", "bana", "sayfa", "sayfayi", "sonucları", "ver", "verir"];

    public static bool IsMore(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var tokens = Fold(message)
            .Split([' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !Filler.Contains(t))
            .ToArray();
        return tokens.Length > 0 && Core.Contains(string.Join(' ', tokens));
    }

    // Türkçe-duyarlı sadeleştirme + noktalama temizliği: İ/I/ı→i, diakritik→ASCII, harf-olmayan→boşluk.
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
                _ when char.IsLetter(c) => char.ToLowerInvariant(c),
                _ => ' ', // noktalama/rakam → ayraç (apostrof, ek, soru işareti...)
            };
        }
        return new string(chars);
    }
}
