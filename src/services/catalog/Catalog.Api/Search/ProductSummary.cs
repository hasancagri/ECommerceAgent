namespace Catalog.Api.Search;

// 011: arama sonucu künye projeksiyonu (keşif kartı). Tüm arama slice'ları (keyword/semantik/hibrit)
// aynı görünümü döner. Fiyat liste fiyatı (DiscountRate ayrı — okuyan indirimli fiyatı hesaplar, 003).
public sealed record ProductSummary(
    string Isbn,
    string Name,
    string? ImageUrl,
    decimal? Price,
    decimal? DiscountRate)
{
    public static ProductSummary From(Product p) => new(
        p.Gtin,
        p.Name,
        p.ImageUrl,
        p.Price?.Amount,
        p.DiscountRate);
}
