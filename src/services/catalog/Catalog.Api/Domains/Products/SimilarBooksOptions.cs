namespace Catalog.Api.Domains.Products;

// 021: similar_books boost katsayıları + sonuç sayısı sınırları. Program.cs'e inline veri yasak →
// Options pattern (BindConfiguration opsiyonel; section yoksa bu default'lar geçerli). Tüketici düz T
// enjekte eder. Katsayı ayarı canlı kalibrasyonla yapılır (yeniden derleme değil, config).
public class SimilarBooksOptions
{
    // Objektif yakınlık katkıları (BM25 konu/ton skoruna EKLENİR): aynı yazar > aynı tür > aynı çevirmen.
    public double AuthorBoost { get; set; } = 2.0;
    public double GenreBoost { get; set; } = 1.0;
    public double TranslatorBoost { get; set; } = 0.5;

    // Sonuç sayısı: istenmezse Default; üst sınır Max (agent 100 isteyemez).
    public int DefaultCount { get; set; } = 5;
    public int MaxCount { get; set; } = 20;

    public int ClampCount(int requested) =>
        requested <= 0 ? DefaultCount : Math.Min(requested, MaxCount);
}
