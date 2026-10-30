namespace Catalog.Api.Seeding;

// 091: books.json kayıt DTO'su (contracts/seed-record.md) — saklanmaz, yalnız okuma. Aynı dosyayı
// File.Api kendi okuyucusuyla (isbn+imageUrl) okur; ortak tip YOK (bilinçli tekrar, sözleşme alan adları).
// Bilinmeyen JSON alanları (tags/specs/familyCode/discountPercent/stock/binding) yok sayılır (additive evrim).
public class SeedBookRecord
{
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> Authors { get; set; } = [];
    // yazar adı → biyografi (Kitapyurdu künyesi); seed get-or-create'te Author.SetBio'ya akar.
    public Dictionary<string, string> AuthorBios { get; set; } = [];
    public List<string> Translators { get; set; } = [];
    public string Publisher { get; set; } = string.Empty;
    public decimal PriceTry { get; set; }
    public int? PageCount { get; set; }
    public DateOnly? PublishedOn { get; set; }
    public string? Language { get; set; }
    public string? ImageUrl { get; set; }
    // kanonik kategori: [üst] ya da [üst, leaf] (canonicalize_categories.py çıktısı). primary.
    public List<string> CategoryPath { get; set; } = [];
    // ek kategori atamaları (çoklu-kategori); her biri [üst] ya da [üst, leaf].
    public List<List<string>> RelatedCategoryPaths { get; set; } = [];
    public string Description { get; set; } = string.Empty;

    /// <summary>Satırın ürün doğurabilirliğini saf doğrular — ihlal satırı atlatır (seed sürer):
    /// ISBN boş olamaz (kimlik), fiyat>0 (var oluş invariant'ının ön kapısı).</summary>
    public ResultDomain Validate()
    {
        if (string.IsNullOrWhiteSpace(Isbn))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Isbn), Code = CatalogResourceConstants.VALUE_EMPTY });
        if (PriceTry <= 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(PriceTry), Code = CatalogResourceConstants.PRODUCT_PRICE_REQUIRED });
        return ResultDomain.Ok();
    }
}