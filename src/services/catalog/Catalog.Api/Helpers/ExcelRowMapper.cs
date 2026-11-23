using System.Globalization;

namespace Catalog.Api.Helpers;

// 004: satır → künye eşlemesi SAF birimi (test-first — ExcelRowMapperTests). ClosedXML'e dokunmaz:
// girdi "sütun adı → ham hücre değeri" sözlüğü; ImportJobProcess ClosedXML satırını bu sözlüğe çevirir.
// Eşleme kuralları research R2 tablosu: okunmayan sütunlar (stok/indirim/okuma süresi) BURADA DA yok.
public static class ExcelRowMapper
{
    // Satır sonucu: Row dolu = geçerli; Error dolu = sebepli satır hatası (iş durmaz, rapora düşer).
    public sealed record MapResult(ImportRow? Row, string? Error);

    public static MapResult Map(string sheetName, int rowNumber, IReadOnlyDictionary<string, object?> cells)
    {
        var isbn = Text(cells, "ISBN");
        if (string.IsNullOrWhiteSpace(isbn))
            return new MapResult(null, "ISBN boş");

        var name = Text(cells, "Kitap Adı");
        if (string.IsNullOrWhiteSpace(name))
            return new MapResult(null, "Kitap adı boş");

        var row = new ImportRow
        {
            SheetName = sheetName,
            RowNumber = rowNumber,
            Name = name.Trim(),
            Isbn = isbn.Trim(),
            Description = Text(cells, "Açıklama")?.Trim() ?? string.Empty,
            AuthorNames = SplitNames(Text(cells, "Yazar")),
            AuthorBio = Text(cells, "Yazar Açıklaması")?.Trim(),
            PublisherName = sheetName.Trim(), // R1: sayfa adı = yayınevi
            Translators = SplitNames(Text(cells, "Çevirmen")),
            PageCount = Int(cells, "Sayfa Sayısı"),
            Language = Text(cells, "Dil")?.Trim(),
            PublishedOn = Date(Text(cells, "Yayın Tarihi")),
            Binding = ParseBinding(Text(cells, "Cilt Tipi")),
            // 021: v11 taksonomi kolonları — HAM parçalar (kanonikleştirme AGGREGATE'te, SetEnrichment).
            // tur tek değer; ton/konu `;`-ayraçlı çoklu; guven kesir (virgül toleranslı parse).
            Genre = Text(cells, "tur")?.Trim(),
            MoodsRaw = SplitTaxonomy(Text(cells, "ton")),
            AudienceLevelRaw = Text(cells, "seviye")?.Trim(),
            ThemesRaw = SplitTaxonomy(Text(cells, "konu")),
            Confidence = Decimal(cells, "guven"),
            // 021: Kitapyurdu "Stok Durumu" ikili sinyali — "SATIŞ YOK" prefix'i satış-dışı (tükenmiş/
            // satışta değil); diğer tüm metin (kargo/temin) ve boş hücre satışta (iyimser default).
            InStock = !StartsWithSatisYok(Text(cells, "Stok Durumu")),
            // 010: İndirim oranı — "İndirim %" ZATEN kesir (0.285 = %28.5), dönüşüm YOK. >0 ise taşı,
            // aksi null (indirim yok). Aralık guard'ı (≤0.90) worker'da yayın anında.
            DiscountRate = Decimal(cells, "İndirim %") is { } dr && dr > 0 ? dr : null,
            Price = PriceWithFallback(cells),
            Rating = RoundedRating(Decimal(cells, "Puan")),
            RatingCount = Int(cells, "Puan Adedi") ?? 0,
            PurchaseCount = Int(cells, "Satın Alma Adedi") ?? 0,
            ToReadCount = Int(cells, "Okuyacağım") ?? 0,
            ReadingCount = Int(cells, "Okuyorum") ?? 0,
            ReadCount = Int(cells, "Okudum") ?? 0,
            FavoriteCount = Int(cells, "Favori") ?? 0,
            CoverUrl = Text(cells, "Kapak URL")?.Trim(),
        };
        return new MapResult(row, null);
    }

    private static string? Text(IReadOnlyDictionary<string, object?> cells, string column)
        => cells.TryGetValue(column, out var v) ? v?.ToString() : null;

    private static decimal? Decimal(IReadOnlyDictionary<string, object?> cells, string column)
    {
        if (!cells.TryGetValue(column, out var v) || v is null) return null;
        return v switch
        {
            decimal d => d,
            double d => (decimal)d,
            int i => i,
            long l => l,
            _ => decimal.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed : null,
        };
    }

    private static int? Int(IReadOnlyDictionary<string, object?> cells, string column)
        => Decimal(cells, column) is { } d ? (int)d : null;

    // Fiyat: Liste Fiyatı → yoksa Kitapyurdu Fiyatı → ikisi de yok/0 = fiyatsız (satış-dışı doğum, 001).
    private static decimal? PriceWithFallback(IReadOnlyDictionary<string, object?> cells)
    {
        var price = Decimal(cells, "Liste Fiyatı (₺)") ?? Decimal(cells, "Kitapyurdu Fiyatı (₺)");
        return price is > 0 ? price : null;
    }

    private static int? RoundedRating(decimal? rating)
        => rating is { } r ? (int)Math.Round(r, MidpointRounding.AwayFromZero) : null;

    private static List<string> SplitNames(string? cell)
        => string.IsNullOrWhiteSpace(cell)
            ? new List<string>()
            : cell.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Distinct()
                .ToList();

    // 021: taksonomi çoklu değer — YALNIZ `;` ayraç (değerlerin kendisinde '/' ve ',' VAR,
    // ör. "sanat/yaratıcılık" — SplitNames'in virgülü bunları bölerdi). Kanonikleştirme yok (aggregate).
    private static List<string> SplitTaxonomy(string? cell)
        => string.IsNullOrWhiteSpace(cell)
            ? new List<string>()
            : cell.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

    // "SATIŞ YOK*" (TÜKENMİŞ / Satışta Değil) = satış-dışı; boş/diğer metin satışta.
    private static bool StartsWithSatisYok(string? stockStatus)
        => stockStatus?.Trim().StartsWith("SATIŞ YOK", StringComparison.OrdinalIgnoreCase) ?? false;

    private static BindingType ParseBinding(string? cell) => cell?.Trim().ToUpperInvariant() switch
    {
        "KARTON KAPAK" => BindingType.KartonKapak,
        "CİLTLİ" or "CILTLI" => BindingType.Ciltli,
        "KENDİNDEN KAPAK" or "KENDINDEN KAPAK" => BindingType.KendindenKapak,
        "PLASTİK" or "PLASTIK" => BindingType.Plastik,
        _ => BindingType.Unknown,
    };

    private static DateOnly? Date(string? cell)
    {
        if (string.IsNullOrWhiteSpace(cell)) return null;
        if (DateOnly.TryParseExact(cell.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            return date;
        if (DateTime.TryParse(cell.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return DateOnly.FromDateTime(dt);
        return null;
    }
}

// Eşlenmiş satır — süreç bunu aggregate çağrılarına çevirir (ImportJobProcess).
public sealed class ImportRow
{
    public string SheetName { get; init; } = default!;
    public int RowNumber { get; init; }
    public string Name { get; init; } = default!;
    public string Isbn { get; init; } = default!;
    public string Description { get; init; } = string.Empty;
    public List<string> AuthorNames { get; init; } = new();
    public string? AuthorBio { get; init; }
    public string PublisherName { get; init; } = default!;
    public List<string> Translators { get; init; } = new();
    public decimal? DiscountRate { get; init; }
    // 021: v11 taksonomi (ham — kanonikleştirme SetEnrichment'te) + stok sinyali.
    public string? Genre { get; init; }
    public List<string> MoodsRaw { get; init; } = new();
    public string? AudienceLevelRaw { get; init; }
    public List<string> ThemesRaw { get; init; } = new();
    public decimal? Confidence { get; init; }
    public bool InStock { get; init; } = true;
    public int? PageCount { get; init; }
    public string? Language { get; init; }
    public DateOnly? PublishedOn { get; init; }
    public BindingType Binding { get; init; }
    public decimal? Price { get; init; }
    public int? Rating { get; init; }
    public int RatingCount { get; init; }
    public int PurchaseCount { get; init; }
    public int ToReadCount { get; init; }
    public int ReadingCount { get; init; }
    public int ReadCount { get; init; }
    public int FavoriteCount { get; init; }
    public string? CoverUrl { get; init; }
}
