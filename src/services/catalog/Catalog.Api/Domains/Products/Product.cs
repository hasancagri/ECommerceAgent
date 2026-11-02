namespace Catalog.Api.Domains.Products;

/// <summary>
/// Ürün — Catalog BC'nin kök aggregate'i. 095: sade Kitapyurdu künye modeli. Kimlik Gtin (ISBN,
/// unique-index); TEK leaf kategori (üst kategori ürüne DENORMALIZE EDİLMEZ — ağaçtan çözülür)
/// + editoryal RelatedCategoryIds (ata zinciriyle karışmaz). 001: kitap fiyatsız/yazarsız doğabilir —
/// katalogda görünür ama satışa uygun değildir (IsSalable); fiyat sonradan atanır ve serbest değişir
/// (>0 guard). Silme-yok kararı (IsDeleted kontratta hep false) sürer. Koleksiyonlar private,
/// mutasyon yalnız davranış metotlarından.
/// </summary>
public class Product : AggregateRoot
{
    public string Name { get; private set; } = default!;
    // ISBN — ürün kimliği (unique-index, MartenExtensions). Toplu giriş Gtin-varsa-atlar.
    public string Gtin { get; private set; } = default!;
    // Tek açıklama alanı (095: Short/Full birleşti).
    public string Description { get; private set; } = string.Empty;

    // 001: null = fiyatsız (satış-dışı doğum); değer varsa Amount > 0 (Create/SetPrice guard'ları).
    public Money? Price { get; private set; }

    // 002: StockChanged-fed adet KOPYASI — otorite Stock BC (tek yazar), buradaki değer türetilmiş
    // (saniyeler bayat olabilir; vitrin için yeter, para alınan an Stock'tan otoriter düşülür).
    public int StockCount { get; private set; }

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
    public BindingType Binding { get; private set; } = BindingType.Unknown;

    // 001: popülerlik sayaçları — dış kaynak anlık görüntüsü (bizim canlı verimiz DEĞİL);
    // keşif/sıralama sinyali. Satışa-uygunluk kararına GİRMEZ.
    public int? Rating { get; private set; } // 0-5; null = hiç puanlanmamış
    public int RatingCount { get; private set; }
    public int PurchaseCount { get; private set; }
    public int ToReadCount { get; private set; }
    public int ReadingCount { get; private set; }
    public int ReadCount { get; private set; }
    public int FavoriteCount { get; private set; }
    private readonly List<string> _translators = new();
    public IReadOnlyList<string> Translators => _translators;

    /// <summary>Satışa-uygunluk — TEK tanım (001+002): fiyat>0 VE stok>0 VE ≥1 yazar. Türetilmiş
    /// saf getter, hiçbir yere kopyalanmaz; stok bacağı StockChanged-fed kopyadan okunur.</summary>
    public bool IsSalable => Price is { Amount: > 0 } && StockCount > 0 && _authorIds.Count > 0;

    /// <summary>Stok adet kopyasını yazar (002). Çağıranı YALNIZ StockChanged tüketici kablosu —
    /// kullanıcı/admin yolu değildir; otorite Stock BC. Negatif reddedilir; idempotent (mutlak değer).</summary>
    public ResultDomain SetStockCount(int stockCount)
    {
        if (stockCount < 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(StockCount), Code = CatalogResourceConstants.VALUE_INVALID });
        StockCount = stockCount;
        return ResultDomain.Ok();
    }

    private Product()
    {
    }

    /// <summary>Yeni ürün oluşturur. Guard'lar: ad/Gtin boş olamaz; fiyat OPSİYONEL (001) —
    /// verilmezse ürün fiyatsız (satışa kapalı) doğar, verilirse >0 olmalı. Ürün doğduğu an
    /// katalogda görünürdür (taslak/yayın ekseni yok); satın alınabilirlik IsSalable'dan okunur.</summary>
    public static ResultDomain<Product> Create(string name, string gtin, Money? price, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Product>.Error(new MessageItem
            { Property = nameof(Name), Code = CatalogResourceConstants.PRODUCT_NAME_REQUIRED });
        if (string.IsNullOrWhiteSpace(gtin))
            return ResultDomain<Product>.Error(new MessageItem
            { Property = nameof(Gtin), Code = CatalogResourceConstants.PRODUCT_GTIN_REQUIRED });
        if (price is not null && price.Amount <= 0)
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

    // Rename SİLİNDİ (001, kullanıcı kararı): tek işi boş-ad kontrolüydü, yüzeyi yok.
    // Admin düzenleme ekranı ad değişikliği isterse davranış o gün geri gelir.

    /// <summary>Açıklamayı günceller (tek alan). Hep Ok.</summary>
    public ResultDomain SetDescription(string description)
    {
        Description = description;
        return ResultDomain.Ok();
    }

    /// <summary>Fiyatı atar/değiştirir (001 — 097 "doğumda sabit" revize edildi). Atanan değer >0
    /// olmalı; geçersizse mevcut fiyat değişmez. Fiyatı geri "fiyatsız"a çeviren yol YOKTUR.</summary>
    public ResultDomain SetPrice(Money price)
    {
        if (price.Amount <= 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Price), Code = CatalogResourceConstants.PRODUCT_PRICE_REQUIRED });
        Price = price;
        return ResultDomain.Ok();
    }

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
    /// GEÇERLİ (001 — yazarsız kitap katalogda yaşar, satışa uygun sayılmaz); Id'ler tekilleştirilir.</summary>
    public ResultDomain SetAuthors(IEnumerable<Guid> authorIds)
    {
        var deduped = authorIds.Distinct().ToList();
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

    /// <summary>Cilt tipini atar (001). Hep Ok — Unknown dahil her enum değeri geçerli.</summary>
    public ResultDomain SetBinding(BindingType binding)
    {
        Binding = binding;
        return ResultDomain.Ok();
    }

    /// <summary>Popülerlik sayaçlarını TAM-DEĞİŞTİRME ile yazar (001). Guard'lar: puan 0-5 (null =
    /// hiç puanlanmamış), sayaçlar negatif olamaz; geçersizde hiçbir alan değişmez. Sayaçlar
    /// satışa-uygunluk kararına girmez.</summary>
    public ResultDomain SetPopularity(int? rating, int ratingCount, int purchaseCount,
        int toReadCount, int readingCount, int readCount, int favoriteCount)
    {
        if (rating is < 0 or > 5)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Rating), Code = CatalogResourceConstants.VALUE_INVALID });
        if (ratingCount < 0 || purchaseCount < 0 || toReadCount < 0
            || readingCount < 0 || readCount < 0 || favoriteCount < 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(RatingCount), Code = CatalogResourceConstants.VALUE_INVALID });

        Rating = rating;
        RatingCount = ratingCount;
        PurchaseCount = purchaseCount;
        ToReadCount = toReadCount;
        ReadingCount = readingCount;
        ReadCount = readCount;
        FavoriteCount = favoriteCount;
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

// Enum aggregate dosyasında yaşar (konvansiyon). Kaynak kümedeki 4 değer + Unknown (bilinmeyen/boş).
public enum BindingType
{
    Unknown = 0,
    KartonKapak = 1,
    Ciltli = 2,
    KendindenKapak = 3,
    Plastik = 4,
}
