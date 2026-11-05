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

        var (parent, leaf) = ParseCategoryPath(Text(cells, "Kategori"));

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
            ParentCategoryName = parent,
            LeafCategoryName = leaf,
            RelatedCategoryLeafNames = ParseRelatedLeaves(Text(cells, "İlgili Kategoriler")),
            PageCount = Int(cells, "Sayfa Sayısı"),
            Language = Text(cells, "Dil")?.Trim(),
            PublishedOn = Date(Text(cells, "Yayın Tarihi")),
            Binding = ParseBinding(Text(cells, "Cilt Tipi")),
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

    // "Kitap > Üst > Leaf" → (Üst, Leaf); "Kitap > X" → (null, X); boş → (null, null) = "Genel" fallback.
    private static (string? Parent, string? Leaf) ParseCategoryPath(string? path)
    {
        var segments = SplitPath(path);
        return segments.Count switch
        {
            0 => (null, null),
            1 => (null, segments[^1]),
            _ => (segments[^2], segments[^1]),
        };
    }

    // İlgili kategoriler: her girdinin LEAF'i alınır (editoryal liste leaf-bazlı; ata zinciri karışmaz).
    private static List<string> ParseRelatedLeaves(string? cell)
    {
        if (string.IsNullOrWhiteSpace(cell)) return new List<string>();
        return cell.Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => SplitPath(p))
            .Where(s => s.Count > 0)
            .Select(s => s[^1])
            .Distinct()
            .ToList();
    }

    // "Kitap" kök düğümü atılır — ağaç ≤2 seviye (kök → leaf) kuralına oturur.
    private static List<string> SplitPath(string? path)
        => string.IsNullOrWhiteSpace(path)
            ? new List<string>()
            : path.Split('>', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0 && !s.Equals("Kitap", StringComparison.OrdinalIgnoreCase))
                .ToList();

    private static List<string> SplitNames(string? cell)
        => string.IsNullOrWhiteSpace(cell)
            ? new List<string>()
            : cell.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Distinct()
                .ToList();

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
    public string? ParentCategoryName { get; init; }
    public string? LeafCategoryName { get; init; }
    public List<string> RelatedCategoryLeafNames { get; init; } = new();
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
