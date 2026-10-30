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

    // Sunum/vitrin politikaları (katalog kararı — başka BC'ye ait değil).
    // 091: Published/taslak ekseni söküldü — ürün var = canlı; fiyat>0 var oluş invariant'ı.
    public bool ShowOnHomepage { get; private set; }
    public bool MarkAsNew { get; private set; }
    public bool AllowCustomerReviews { get; private set; } = true;

    // K4: model çoklu atama taşır; ingestion tek kategori atar — Categories[0] = primary (event'e gider).
    private readonly List<ProductCategoryAssignment> _categories = new();
    public IReadOnlyList<ProductCategoryAssignment> Categories => _categories;

    // K9: feed etiket vermez; koleksiyon bu feature'da boş yaşar.
    private readonly List<Guid> _tagIds = new();
    public IReadOnlyList<Guid> TagIds => _tagIds;

    // ⊕ 052: kitap künyesi — çok-yazar (Id listesi, çok-çok) + tek yayınevi (çok-bir). Yazarlar/yayınevi
    // Catalog aggregate'leri, Id ile referanslanır (İLKE II). Görsel URL feed'den gelir; serving servisi yok.
    private readonly List<Guid> _authorIds = new();
    public IReadOnlyList<Guid> AuthorIds => _authorIds;
    public Guid PublisherId { get; private set; }
    public string? ImageUrl { get; private set; }

    // ⊕ Kitapyurdu kitap künyesi — yüksek-kardinalite, facet DEĞİL (düz alan, SpecificationAttribute değil).
    // Dil kapalı-liste ama aggregate kurmadan düz string tutulur, yayında ProductSpec'e projekte edilir (B).
    public int? PageCount { get; private set; }
    public DateOnly? PublishedOn { get; private set; }
    public string? Language { get; private set; }
    private readonly List<string> _translators = new();
    public IReadOnlyList<string> Translators => _translators;

    private Product()
    {
    }

    /// <summary>Yeni ürün oluşturur. 091: fiyat>0 var oluş invariant'ı — fiyatsız ürün doğamaz
    /// (yayın kapısı kalktı, kapı var oluşa taşındı); ürün doğduğu an canlıdır.</summary>
    public static ResultDomain<Product> Create(string name, string sku, ProductType type, Money price,
        string shortDescription, string fullDescription)
    {
        if (price.Amount <= 0)
            return ResultDomain<Product>.Error(new MessageItem
            { Property = nameof(Price), Code = CatalogResourceConstants.PRODUCT_PRICE_REQUIRED });

        return ResultDomain<Product>.Ok(new Product
        {
            Name = name,
            Sku = sku,
            Type = type,
            Price = price,
            ShortDescription = shortDescription,
            FullDescription = fullDescription,
        });
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

    /// <summary>Liste fiyatını değiştirir. 091: fiyat>0 invariant'ı (negatif guard Money VO'da,
    /// sıfır guard'ı burada — satılamaz kart doğamaz/kalamaz).</summary>
    public ResultDomain SetPrice(Money price)
    {
        if (price.Amount <= 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Price), Code = CatalogResourceConstants.PRODUCT_PRICE_REQUIRED });
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

    /// <summary>⊕ Kitap künyesi meta alanlarını yazar (sayfa/yayın tarihi/dil). Hepsi opsiyonel,
    /// yoksa null; doğrulama yok (facet değil, kardinalite yüksek). Hep Ok.</summary>
    public ResultDomain SetBookMeta(int? pageCount, DateOnly? publishedOn, string? language)
    {
        PageCount = pageCount;
        PublishedOn = publishedOn;
        Language = string.IsNullOrWhiteSpace(language) ? null : language.Trim();
        return ResultDomain.Ok();
    }

    /// <summary>⊕ Çevirmenleri TAM-DEĞİŞTİRME ile yazar (çevirmen aggregate DEĞİL — gözatma yok, bilinçli
    /// asimetri). Trim + boş-ele + dedupe (sırayı korur); boş liste geçerli (çeviri değil). Hep Ok.</summary>
    public ResultDomain SetTranslators(IEnumerable<string> translators)
    {
        var cleaned = translators
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct()
            .ToList();
        _translators.Clear();
        _translators.AddRange(cleaned);
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