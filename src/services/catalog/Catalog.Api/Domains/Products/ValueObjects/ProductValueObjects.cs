namespace Catalog.Api.Domains.Products.ValueObjects;

// Product aggregate'inin value object'leri — aggregate başına TEK dosya (VO başına ayrı dosya açılmaz).

/// <summary>
/// Para değeri (tutar + para birimi). Değeriyle tanımlı value object — kimliği yok.
/// Fiyat çıplak decimal yerine birim taşıyan VO'ya sarılır (invariant: tutar >= 0).
/// Dış kontratlara (ProductChangedEvent, REST response) yalnız Amount sızar (040 K2).
/// </summary>
public record Money
{
    public decimal Amount { get; private init; }
    public string Currency { get; private init; } = "TRY";

    private Money() { }

    /// <summary>Tutar negatifse null döner (guard çağıranda). Aksi halde Money üretir.</summary>
    public static Money? Create(decimal amount, string currency = "TRY")
    {
        if (amount < 0)
            return null;
        return new Money { Amount = amount, Currency = currency };
    }

    public static Money Zero(string currency = "TRY") => new() { Amount = 0, Currency = currency };
}

/// <summary>
/// Ürünün fiziksel ölçüleri (ağırlık + en/boy/yükseklik). Kargo bunları tüketir, ama ölçü ürünün
/// fiziksel niteliği olduğu için Catalog Product aggregate'inde durur (nopCommerce paritesi).
/// feed ölçü vermez — Empty varsayılanla yaşar, hiçbir akışı bloklamaz.
/// </summary>
public record ProductDimensions
{
    public decimal Weight { get; private init; }
    public decimal Length { get; private init; }
    public decimal Width { get; private init; }
    public decimal Height { get; private init; }

    private ProductDimensions() { }

    /// <summary>Negatif ölçü varsa null döner (guard çağıranda).</summary>
    public static ProductDimensions? Create(decimal weight, decimal length, decimal width, decimal height)
    {
        if (weight < 0 || length < 0 || width < 0 || height < 0)
            return null;
        return new ProductDimensions { Weight = weight, Length = length, Width = width, Height = height };
    }

    public static ProductDimensions Empty() => new();
}

/// <summary>
/// Arama motoru meta verisi (title/keywords/description). nopCommerce'te Product/Category/Tag hepsinde
/// tekrar eden alanlar; tek VO'ya toplandı (staging konum düzeni: Products/ValueObjects altında,
/// Category ve ProductTag da buradan kullanır). 040: feed'den dolmaz, Empty varsayılanla yaşar.
/// </summary>
public record SeoMetadata
{
    public string? MetaTitle { get; private init; }
    public string? MetaKeywords { get; private init; }
    public string? MetaDescription { get; private init; }

    private SeoMetadata() { }

    public static SeoMetadata Create(string? metaTitle, string? metaKeywords, string? metaDescription) =>
        new() { MetaTitle = metaTitle, MetaKeywords = metaKeywords, MetaDescription = metaDescription };

    public static SeoMetadata Empty() => new();
}

/// <summary>
/// Ürünün bir kategoriye atanması (nopCommerce ProductCategory eşlemesi). Product ile Category
/// çok-a-çok; eşleme öne-çıkan (featured) bayrağı + sıralama taşır. Product aggregate'inin child'ı,
/// mutasyon yalnız Product.AssignToCategory/RemoveFromCategory üzerinden geçer.
/// K4: model çoklu atama taşır; ingestion tek kategori atar, ilk atama = primary (event'e gider).
/// </summary>
public record ProductCategoryAssignment
{
    public Guid CategoryId { get; private init; }
    public bool IsFeatured { get; private init; }
    public int DisplayOrder { get; private init; }

    private ProductCategoryAssignment() { }

    public static ProductCategoryAssignment Create(Guid categoryId, bool isFeatured, int displayOrder) =>
        new() { CategoryId = categoryId, IsFeatured = isFeatured, DisplayOrder = displayOrder };
}

// ProductSpecificationAssignment SÖKÜLDÜ — SpecificationAttribute facet sistemi kaldırıldı
// (downstream Storefront/ES gitti); ihtiyaç doğarsa 043 deseniyle geri eklenir.