namespace Catalog.Api.Domains.Products;

/// <summary>
/// Ürün — Catalog BC'nin kök aggregate'i. 095: PDF-only Kitapyurdu sade künye modeli — nop mirası
/// alanlar (Sku/Dimensions/Type/Seo/vitrin bayrakları/Tag/çoklu-kategori) söküldü. Kimlik Gtin (ISBN,
/// unique-index); TEK leaf kategori (üst kategori ürüne DENORMALIZE EDİLMEZ — ağaçtan çözülür, FR-004)
/// + editoryal RelatedCategoryIds (ata zinciriyle karışmaz, FR-005). Fiyat>0 var oluş invariant'ı (091)
/// ve silme-yok kararı (IsDeleted kontratta hep false) sürer. Koleksiyonlar private, mutasyon yalnız
/// davranış metotlarından.
/// </summary>
public class Product : AggregateRoot
{
    public string Name { get; private set; } = default!;
    // ISBN — ürün kimliği (unique-index, MartenExtensions). Seed Gtin-varsa-atlar.
    public string Gtin { get; private set; } = default!;
    // Tek açıklama alanı (095: Short/Full birleşti); 096 embedding kaynağı olarak korunur.
    public string Description { get; private set; } = string.Empty;

    public Money Price { get; private set; } = Money.Zero();

    // Tek leaf kategori (zorunlu — seed "Genel" fallback'iyle garanti eder).
    public Guid CategoryId { get; private set; }

    // Editoryal "bu kategoriyle DE ilgili" listesi — primary leaf'i ve ata zincirini İÇERMEZ.
    private readonly List<Guid> _relatedCategoryIds = new();
    public IReadOnlyList<Guid> RelatedCategoryIds => _relatedCategoryIds;

    // Kitap künyesi — çok-yazar (Id listesi) + tek yayınevi (çok-bir); Id ile referans (İLKE II).
    private readonly List<Guid> _authorIds = new();
    public IReadOnlyList<Guid> AuthorIds => _authorIds;
    public Guid PublisherId { get; private set; }
    public string? ImageUrl { get; private set; }

    public int? PageCount { get; private set; }
    public DateOnly? PublishedOn { get; private set; }
    public string? Language { get; private set; }
    private readonly List<string> _translators = new();
    public IReadOnlyList<string> Translators => _translators;

    private Product()
    {
    }

    /// <summary>Yeni ürün oluşturur. Guard'lar: ad/Gtin boş olamaz, fiyat>0 var oluş invariant'ı
    /// (fiyatsız ürün doğamaz; ürün doğduğu an canlıdır — taslak/yayın ekseni yok).</summary>
    public static ResultDomain<Product> Create(string name, string gtin, Money price, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Product>.Error(new MessageItem
            { Property = nameof(Name), Code = CatalogResourceConstants.PRODUCT_NAME_REQUIRED });
        if (string.IsNullOrWhiteSpace(gtin))
            return ResultDomain<Product>.Error(new MessageItem
            { Property = nameof(Gtin), Code = CatalogResourceConstants.PRODUCT_GTIN_REQUIRED });
        if (price.Amount <= 0)
            return ResultDomain<Product>.Error(new MessageItem
            { Property = nameof(Price), Code = CatalogResourceConstants.PRODUCT_PRICE_REQUIRED });

        return ResultDomain<Product>.Ok(new Product
        {
            Name = name,
            Gtin = gtin.Trim(),
            Price = price,
            Description = description,
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

    /// <summary>Açıklamayı günceller (tek alan). Hep Ok.</summary>
    public ResultDomain SetDescription(string description)
    {
        Description = description;
        return ResultDomain.Ok();
    }

    // SetPrice SÖKÜLDÜ (097): fiyat doğumda sabit — doğum sonrası fiyat mutasyonu yok
    // (fiyat>0 guard'ı Create'te yaşar).

    /// <summary>Ürünü TEK leaf kategoriye bağlar (tam-değiştirme). Boş Id reddedilir; üst kategori
    /// bilgisi ürüne yazılmaz, yayın anında ağaçtan çözülür (FR-004).</summary>
    public ResultDomain SetCategory(Guid leafCategoryId)
    {
        if (leafCategoryId == Guid.Empty)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(CategoryId), Code = CatalogResourceConstants.PRODUCT_CATEGORY_REQUIRED });
        CategoryId = leafCategoryId;
        return ResultDomain.Ok();
    }

    /// <summary>Editoryal ilgili kategorileri TAM-DEĞİŞTİRME ile yazar. Boş Id'ler ve primary leaf
    /// elenir (ilgili liste ata zinciri/leaf ile karışmaz — FR-005), dedupe; boş liste geçerli. Hep Ok.</summary>
    public ResultDomain SetRelatedCategories(IEnumerable<Guid> categoryIds)
    {
        var cleaned = categoryIds
            .Where(id => id != Guid.Empty && id != CategoryId)
            .Distinct()
            .ToList();
        _relatedCategoryIds.Clear();
        _relatedCategoryIds.AddRange(cleaned);
        return ResultDomain.Ok();
    }

    /// <summary>Kitabın yazar(lar)ını TAM-DEĞİŞTİRME ile atar (çok-çok, Id ile referans). Boş liste
    /// reddedilir (ürün ≥1 yazar); Id'ler tekilleştirilir.</summary>
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

    /// <summary>Kitabın tek yayınevini atar (çok-bir, zorunlu). Boş Id reddedilir.</summary>
    public ResultDomain SetPublisher(Guid publisherId)
    {
        if (publisherId == Guid.Empty)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(publisherId), Code = CatalogResourceConstants.VALUE_EMPTY });
        PublisherId = publisherId;
        return ResultDomain.Ok();
    }

    /// <summary>Kapak URL'ini atar/temizler (tüketicisi kalmadı — 097 serisi; davranış durur).</summary>
    public ResultDomain SetImage(string? imageUrl)
    {
        ImageUrl = imageUrl;
        return ResultDomain.Ok();
    }

    /// <summary>Kitap künyesi meta alanlarını yazar (sayfa/yayın tarihi/dil). Hepsi opsiyonel,
    /// yoksa null; doğrulama yok. Hep Ok.</summary>
    public ResultDomain SetBookMeta(int? pageCount, DateOnly? publishedOn, string? language)
    {
        PageCount = pageCount;
        PublishedOn = publishedOn;
        Language = string.IsNullOrWhiteSpace(language) ? null : language.Trim();
        return ResultDomain.Ok();
    }

    /// <summary>Çevirmenleri TAM-DEĞİŞTİRME ile yazar (çevirmen aggregate DEĞİL — bilinçli asimetri).
    /// Trim + boş-ele + dedupe (sırayı korur); boş liste geçerli (çeviri değil). Hep Ok.</summary>
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
}
