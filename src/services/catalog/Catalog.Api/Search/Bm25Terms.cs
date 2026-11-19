using System.Text.RegularExpressions;

namespace Catalog.Api.Search;

// 018: BM25 serbest-metin → temiz terim listesi (saf; host'suz test edilebilir, İlke VI). Apostrof/
// özel karakter elenir, ≤2 harf + Türkçe dolgu/soru kelimeleri atılır (doğal-cümle sorgu BM25'i
// kirletmesin — "… listeler misin" başlığında geçen kitaplar eşleşmesin). 011'den (ProductSearchQueries)
// çıkarıldı; birleşik yürütücü bunu çağırır.
public static class Bm25Terms
{
    private const int MinTermLength = 3;

    // Türkçe dolgu/soru kelimeleri — niyet/varlık değil, gürültü.
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "mi","mı","mu","mü","misin","mısın","musun","müsün","misiniz","mısınız",
        "var","yok","bana","benim","için","ile","ve","veya","bir","bu","şu","ki","ya",
        "listeler","listele","listeleyebilir","göster","getir","öner","bul","ara",
        "arıyorum","istiyorum","bulur","getirir","verir","söyler",
        "kitap","kitabı","kitabını","kitaplar","kitapları","kitaplarını","eser","eseri","eserleri",
        "hakkında","nedir","kimdir","kim","ne","nasıl","hangi","lütfen",
    };

    /// <summary>Sorgu metnini BM25 terimlerine çevirir: harf/rakam/boşluk dışı karakterleri eler,
    /// boşlukları tekler, ≤2 harf ve stopword terimleri atar. Orijinal büyük/küçük harf korunur
    /// (BM25 tokenizer index'te katlar); boş/gürültü sorgu boş liste döner.</summary>
    public static IReadOnlyList<string> Extract(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        return Sanitize(query)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= MinTermLength && !StopWords.Contains(t))
            .ToList();
    }

    private static string Sanitize(string query) =>
        Regex.Replace(
            Regex.Replace(query, @"[^\p{L}\p{N}\s]", " "),
            @"\s+", " ").Trim();
}
