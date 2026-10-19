namespace Catalog.Api.Seeding;

// 091: books.json kayıt DTO'su (contracts/seed-record.md) — saklanmaz, yalnız okuma. Aynı dosyayı
// File.Api kendi okuyucusuyla (isbn+imageUrl) okur; ortak tip YOK (bilinçli tekrar, sözleşme alan adları).
// Bilinmeyen JSON alanları (tags/specs/familyCode/discountPercent) yok sayılır (additive evrim kuralı).
public class SeedBookRecord
{
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> Authors { get; set; } = [];
    public string Publisher { get; set; } = string.Empty;
    public decimal PriceTry { get; set; }
    public string? ImageUrl { get; set; }
    public string CategoryMid { get; set; } = string.Empty;
    public string CategoryLeaf { get; set; } = string.Empty;
    public int Stock { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Satırın ürün doğurabilirliğini saf doğrular — ihlal satırı atlatır (seed sürer):
    /// ISBN boş olamaz (kimlik), fiyat>0 (var oluş invariant'ının ön kapısı), stok negatif olamaz.</summary>
    public ResultDomain Validate()
    {
        if (string.IsNullOrWhiteSpace(Isbn))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Isbn), Code = CatalogResourceConstants.VALUE_EMPTY });
        if (PriceTry <= 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(PriceTry), Code = CatalogResourceConstants.PRODUCT_PRICE_REQUIRED });
        if (Stock < 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Stock), Code = CatalogResourceConstants.SEED_STOCK_NEGATIVE });
        return ResultDomain.Ok();
    }
}