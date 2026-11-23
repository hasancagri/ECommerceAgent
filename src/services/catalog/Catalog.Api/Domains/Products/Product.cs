namespace Catalog.Api.Domains.Products;

/// <summary>
/// Ürün — Catalog BC'nin kök aggregate'i. 095: sade Kitapyurdu künye modeli. Kimlik Gtin (ISBN,
/// unique-index). 021: kategori ekseni SÖKÜLDÜ (Category aggregate + CategoryId/RelatedCategoryIds +
/// ağaç) — yerine kapalı-sözlük taksonomi (Genre/Moods/AudienceLevel/Themes, Faz 3) keşif ekseni olur;
/// 009 BookTag etiketleri de SÖKÜLDÜ. 001: kitap fiyatsız/yazarsız doğabilir — katalogda görünür ama
/// satışa uygun değildir (IsSalable); fiyat sonradan atanır ve serbest değişir (>0 guard). Silme-yok
/// kararı (IsDeleted kontratta hep false) sürer. Koleksiyonlar private, mutasyon yalnız davranış metotlarından.
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

    // 003: ProductDiscountChanged-fed ETKİN ORAN kopyası — otorite Discount BC; null = indirim yok.
    // İndirimli fiyat SAKLANMAZ: okuyan Price*(1-DiscountRate) hesaplar (liste fiyatı ezilmez).
    public decimal? DiscountRate { get; private set; }

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

    // 021: kapalı-sözlük taksonomi (enrichment pipeline çıktısı — yazma-anı sınıflandırma, okuma-anı
    // facet). Değerler BookTaxonomy sözlüğünden; geçersiz olan SetEnrichment'ta düşer/"belirsiz"e iner.
    public string Genre { get; private set; } = BookTaxonomy.Unknown;      // tur (tek)
    private readonly List<string> _moods = new();                          // ton (0-3)
    public IReadOnlyList<string> Moods => _moods;
    public string AudienceLevel { get; private set; } = BookTaxonomy.Unknown; // seviye (tek)
    private readonly List<string> _themes = new();                         // konu (0-4)
    public IReadOnlyList<string> Themes => _themes;
    public decimal EnrichmentConfidence { get; private set; }              // güven [0,1]

    // 021: eser kimliği — normalize(yazar)+parantezsiz başlık; benzerlik eser-tekilleştirmesi bundan.
    // İç alan (kullanıcıya/LLM'e çıkmaz). Import'ta WorkKeyNormalizer.Derive ile türetilir.
    public string WorkKey { get; private set; } = string.Empty;

    private const int MaxMoods = 3;
    private const int MaxThemes = 4;

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

    /// <summary>Etkin indirim oranı kopyasını yazar/temizler (003). Çağıranı YALNIZ
    /// ProductDiscountChanged tüketici kablosu; otorite Discount BC. null = indirim yok;
    /// değer %1-%90 aralığında olmalı. Satışa-uygunluğa girmez; idempotent (mutlak değer).</summary>
    public ResultDomain SetDiscountRate(decimal? discountRate)
    {
        if (discountRate is <= 0 or > 0.90m)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(DiscountRate), Code = CatalogResourceConstants.VALUE_INVALID });
        DiscountRate = discountRate;
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

    /// <summary>Kapalı-sözlük taksonomiyi TAM-DEĞİŞTİRME ile yazar (021). Her değer BookTaxonomy ile
    /// kanonikleştirilir (takma-ad eşlenir, karşılıksız DÜŞER — iş durmaz, FR-005); tur/seviye geçersizse
    /// "belirsiz"e iner. Liste alanlar dedupe + kardinalite kırpılır (ton≤3, konu≤4). Güven [0,1] clamp.
    /// Hep Ok (geçersiz değer hata değil, düşürme).</summary>
    public ResultDomain SetEnrichment(string? genre, IEnumerable<string> moods, string? audienceLevel,
        IEnumerable<string> themes, decimal confidence)
    {
        Genre = BookTaxonomy.CanonicalGenre(genre ?? string.Empty) ?? BookTaxonomy.Unknown;
        AudienceLevel = BookTaxonomy.CanonicalAudienceLevel(audienceLevel ?? string.Empty) ?? BookTaxonomy.Unknown;

        _moods.Clear();
        _moods.AddRange(moods
            .Select(BookTaxonomy.CanonicalMood)
            .Where(m => m is not null)
            .Select(m => m!)
            .Distinct()
            .Take(MaxMoods));

        _themes.Clear();
        _themes.AddRange(themes
            .Select(BookTaxonomy.CanonicalTheme)
            .Where(t => t is not null)
            .Select(t => t!)
            .Distinct()
            .Take(MaxThemes));

        EnrichmentConfidence = Math.Clamp(confidence, 0m, 1m);
        return ResultDomain.Ok();
    }

    /// <summary>Eser kimliğini yazar (021). Çağıranı import — WorkKeyNormalizer.Derive çıktısını alır.
    /// Boş değer geçerli (yazarsız/başlıksız kenar). Hep Ok.</summary>
    public ResultDomain SetWorkKey(string workKey)
    {
        WorkKey = workKey ?? string.Empty;
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
