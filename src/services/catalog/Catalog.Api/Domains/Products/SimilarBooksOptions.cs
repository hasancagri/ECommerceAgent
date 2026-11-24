namespace Catalog.Api.Domains.Products;

// 027 (V2 vektör): similar_books sonuç sayısı sınırları. 021 boost katsayıları (AuthorBoost/GenreBoost/
// TranslatorBoost) SÖKÜLDÜ — V2 saf vektör kNN, objektif boost yok (vektör tür/ton/konuyu kodluyor).
// Options pattern (BindConfiguration opsiyonel; section yoksa default). Tüketici düz T.
public class SimilarBooksOptions
{
    // Sonuç sayısı: istenmezse Default; üst sınır Max (agent 100 isteyemez).
    public int DefaultCount { get; set; } = 5;
    public int MaxCount { get; set; } = 20;

    public int ClampCount(int requested) =>
        requested <= 0 ? DefaultCount : Math.Min(requested, MaxCount);
}
