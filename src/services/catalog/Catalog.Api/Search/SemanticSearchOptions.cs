namespace Catalog.Api.Search;

// 026: semantik arama eşiği + sonuç sayısı. Program.cs'e inline veri yasak → Options (BindConfiguration
// opsiyonel; section yoksa default). Tüketici düz T. Eşik canlı kalibre edilir (yeniden derleme değil).
public class SemanticSearchOptions
{
    // Kosinüs mesafesi eşiği — üstü alakasız sayılır, DÖNMEZ (alakasız doldurma yok, SC-004). 067 canlı
    // kalibrasyonu 0.68 (Türkçe sorgu ↔ açıklama); 008 Türkçe açıklamalarıyla yeniden kalibre edilebilir.
    public double MaxCosineDistance { get; set; } = 0.68;

    public int DefaultCount { get; set; } = 10;
    public int MaxCount { get; set; } = 20;

    public int ClampCount(int requested) =>
        requested <= 0 ? DefaultCount : Math.Min(requested, MaxCount);
}
