namespace Catalog.Api.Domains.Products;

/// <summary>
/// Ürün — Catalog BC'nin kök aggregate'i (040: staging CustomNopCommerce'ten zengin model extract'i).
/// nopCommerce'in ~100 alanlık god-entity'si staging'de BÖLÜNMÜŞTÜ: envanter → Stock BC, indirim →
/// (silindi), vs. Product'ta yalnız KATALOG kimliği + sunum + liste fiyatı + ölçü + SEO + kategori/etiket
/// eşlemesi kalır. Ana repoya özgü ⊕ alanlar: AuthorIds + PublisherId (052 kitap künyesi), ImageUrl (K7). Tutarlılık sınırı köktür:
/// koleksiyonlar private, mutasyon yalnız davranış metotlarından.
/// kararları sürer: ürün silme yolu yok (IsDeleted event kontratı gereği durur, hep false yayınlanır).
/// </summary>
public class Product : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string ShortDescription { get; private set; } = string.Empty;
    public string FullDescription { get; private set; } = string.Empty;

    public string Sku { get; private set; } = default!;
    // K3: Gtin modelde hazır, feed kontratı değişmediği için boş yaşar — 041 (buy-box) dolduracak.
    public string? Gtin { get; private set; }
    public string? ManufacturerPartNumber { get; private set; }
    // varyant ailesi kodu (feed'den akan opak gruplama kimliği; null = ailesiz).
    public string? FamilyCode { get; private set; }

    public ProductType Type { get; private set; }
    // Grouped ürünün çocukları üst ürüne bu Id ile bağlanır (Simple üründe null, K11: akış yok).
    public Guid? ParentGroupedProductId { get; private set; }

    public Money Price { get; private set; } = Money.Zero();
    public ProductDimensions Dimensions { get; private set; } = ProductDimensions.Empty();
    public SeoMetadata Seo { get; private set; } = SeoMetadata.Empty();

    // Sunum/vitrin politikaları (katalog kararı — başka BC'ye ait değil). K8: yazım yolu publish eder.
    public bool Published { get; private set; }
    public bool ShowOnHomepage { get; private set; }
    public bool MarkAsNew { get; private set; }
    public bool AllowCustomerReviews { get; private set; } = true;

    // K4: model çoklu atama taşır; ingestion tek kategori atar — Categories[0] = primary (event'e gider).
    private readonly List<ProductCategoryAssignment> _categories = new();
    public IReadOnlyList<ProductCategoryAssignment> Categories => _categories;

    // K9: feed etiket vermez; koleksiyon bu feature'da boş yaşar.
    private readonly List<Guid> _tagIds = new();
    public IReadOnlyList<Guid> TagIds => _tagIds;

    // kanonik özellik atamaları (Renk=Siyah...); upsert tam-değiştirme ile yazar.
    private readonly List<ProductSpecificationAssignment> _specifications = new();
    public IReadOnlyList<ProductSpecificationAssignment> Specifications => _specifications;

    // ⊕ 052: kitap künyesi — çok-yazar (Id listesi, çok-çok) + tek yayınevi (çok-bir). Yazarlar/yayınevi
    // Catalog aggregate'leri, Id ile referanslanır (İLKE II). Görsel URL feed'den gelir; serving servisi yok.
    private readonly List<Guid> _authorIds = new();
    public IReadOnlyList<Guid> AuthorIds => _authorIds;
    public Guid PublisherId { get; private set; }
    public string? ImageUrl { get; private set; }

    private Product()
    {
    }

    /// <summary>Yeni ürün oluşturur. Ad/SKU zorunluluğu + fiyat guard'ı handler/VO'da yapılır (staging
    /// konvansiyonu: factory düz aggregate döner).</summary>
    public static Product Create(string name, string sku, ProductType type, Money price,
        string shortDescription, string fullDescription)
    {
        return new Product
        {
            Name = name,
            Sku = sku,
            Type = type,
            Price = price,
            ShortDescription = shortDescription,
            FullDescription = fullDescription,
        };
    }

    /// <summary>Ürün adını değiştirir. Boş ad reddedilir.</summary>
    public ResultDomain Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.PRODUCT_NAME_REQUIRED });
        Name = name;
        return ResultDomain.Ok();
    }

    /// <summary>Kısa + tam açıklamayı günceller.</summary>
    public ResultDomain UpdateDescriptions(string shortDescription, string fullDescription)
    {
        ShortDescription = shortDescription;
        FullDescription = fullDescription;
        return ResultDomain.Ok();
    }

    /// <summary>Liste fiyatını değiştirir (negatif guard Money VO'da).</summary>
    public ResultDomain SetPrice(Money price)
    {
        Price = price;
        return ResultDomain.Ok();
    }

    /// <summary>Fiziksel ölçüleri günceller (040: feed'den dolmaz, Empty varsayılanla yaşar).</summary>
    public ResultDomain SetDimensions(ProductDimensions dimensions)
    {
        Dimensions = dimensions;
        return ResultDomain.Ok();
    }

    /// <summary>SEO meta verisini günceller (040: feed'den dolmaz, Empty varsayılanla yaşar).</summary>
    public ResultDomain SetSeo(SeoMetadata seo)
    {
        Seo = seo;
        return ResultDomain.Ok();
    }

    /// <summary>Ürünü satışa/vitrine açar. 051: yayın kapısı = fiyat>0 (fiyatsız = satılamaz kart, reddedilir;
    /// taslak kalır, sonra fiyat gelince yeniden denenir). Kapı aggregate'te (İLKE II).</summary>
    public ResultDomain Publish()
    {
        if (Price.Amount <= 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Price), Code = CatalogResourceConstants.PRODUCT_PRICE_REQUIRED_FOR_PUBLISH });
        Published = true;
        return ResultDomain.Ok();
    }

    /// <summary>Ürünü vitrinden gizler (silmez). 040'ta akış bağlanmaz; 041 buy-box kullanacak.</summary>
    public ResultDomain Unpublish()
    {
        Published = false;
        return ResultDomain.Ok();
    }

    /// <summary>Ürünü bir kategoriye atar. Aynı kategoriye ikinci kez atama reddedilir (invariant).</summary>
    public ResultDomain AssignToCategory(Guid categoryId, bool isFeatured, int displayOrder)
    {
        if (_categories.Any(c => c.CategoryId == categoryId))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(categoryId), Code = CatalogResourceConstants.PRODUCT_CATEGORY_ALREADY_ASSIGNED });
        _categories.Add(ProductCategoryAssignment.Create(categoryId, isFeatured, displayOrder));
        return ResultDomain.Ok();
    }

    /// <summary>Ürünü bir kategoriden çıkarır. Atanmamış kategori reddedilir.</summary>
    public ResultDomain RemoveFromCategory(Guid categoryId)
    {
        var link = _categories.FirstOrDefault(c => c.CategoryId == categoryId);
        if (link is null)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(categoryId), Code = CatalogResourceConstants.PRODUCT_CATEGORY_NOT_ASSIGNED });
        _categories.Remove(link);
        return ResultDomain.Ok();
    }

    /// <summary>Ürüne etiket ekler (idempotent — zaten varsa sessiz geçer).</summary>
    public ResultDomain AddTag(Guid tagId)
    {
        if (!_tagIds.Contains(tagId))
            _tagIds.Add(tagId);
        return ResultDomain.Ok();
    }

    /// <summary>Üründen etiket çıkarır (idempotent).</summary>
    public ResultDomain RemoveTag(Guid tagId)
    {
        _tagIds.Remove(tagId);
        return ResultDomain.Ok();
    }

    /// <summary>Özellik atamalarını TAM-DEĞİŞTİRME ile yazar (043; boş liste = temizleme). Aynı
    /// attribute'un birden çok değeri reddedilir; hatada mevcut atamalar değişmez.</summary>
    public ResultDomain SetSpecifications(IReadOnlyList<ProductSpecificationAssignment> assignments)
    {
        if (assignments.GroupBy(a => a.AttributeId).Any(g => g.Count() > 1))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(assignments), Code = CatalogResourceConstants.SPEC_DUPLICATE_ATTRIBUTE });

        _specifications.Clear();
        _specifications.AddRange(assignments);
        return ResultDomain.Ok();
    }

    /// <summary>045: varyant ailesi kodunu yazar (null/boş = aileden çıkar). Feed'den akan opak kimlik.</summary>
    public ResultDomain SetFamilyCode(string? familyCode)
    {
        FamilyCode = string.IsNullOrWhiteSpace(familyCode) ? null : familyCode.Trim();
        return ResultDomain.Ok();
    }

    /// <summary>⊕ 052: kitabın yazar(lar)ını TAM-DEĞİŞTİRME ile atar (çok-çok, Id ile referans). Boş liste
    /// reddedilir (yayınlanan ürün ≥1 yazar); Id'ler tekilleştirilir (aynı yazar iki kez eklenmez).</summary>
    public ResultDomain SetAuthors(IEnumerable<Guid> authorIds)
    {
        var deduped = authorIds.Distinct().ToList();
        if (deduped.Count == 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(authorIds), Code = CatalogResourceConstants.VALUE_EMPTY });

        _authorIds.Clear();
        _authorIds.AddRange(deduped);
        return ResultDomain.Ok();
    }

    /// <summary>⊕ 052: kitabın tek yayınevini atar (çok-bir, zorunlu). Boş Id reddedilir.</summary>
    public ResultDomain SetPublisher(Guid publisherId)
    {
        if (publisherId == Guid.Empty)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(publisherId), Code = CatalogResourceConstants.VALUE_EMPTY });
        PublisherId = publisherId;
        return ResultDomain.Ok();
    }

    /// <summary>⊕ Görsel URL'ini atar/temizler (feed kaynaklı URL; iç serving servisi yok).</summary>
    public ResultDomain SetImage(string? imageUrl)
    {
        ImageUrl = imageUrl;
        return ResultDomain.Ok();
    }

    /// <summary>⊕ Ürün kimlik alanlarını (Sku/Gtin/MPN) günceller. Boş SKU reddedilir.</summary>
    public ResultDomain SetIdentifiers(string sku, string? gtin, string? manufacturerPartNumber)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(sku), Code = CatalogResourceConstants.PRODUCT_SKU_REQUIRED });
        Sku = sku;
        Gtin = gtin;
        ManufacturerPartNumber = manufacturerPartNumber;
        return ResultDomain.Ok();
    }
}

/// <summary>
/// Ürün tipi. nopCommerce paritesi: Simple (tekil satılan) ve Grouped (variant'ları olan üst ürün).
/// Grouped ürünün çocukları <see cref="Product.ParentGroupedProductId"/> ile üst ürüne bağlanır.
/// K11: feed hep Simple üretir; Grouped alanları pasif taşınır.
/// </summary>
public enum ProductType
{
    Simple = 5,
    Grouped = 10,
}