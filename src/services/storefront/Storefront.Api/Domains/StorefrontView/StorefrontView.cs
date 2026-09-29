namespace Storefront.Api.Domains.StorefrontView;

// Rich aggregate DEGIL: invariant tasimaz — Catalog+Stock'un ProductId-anahtarli
// tek-satirlik (composite) projeksiyonu. Her kaynak YALNIZCA kendi alanlarini yazar; satiri
// herhangi bir kaynak yaratabilir (kismi satir gecerlidir).
public class StorefrontView
{
    private StorefrontView()
    {
    }

    public Guid ProductId { get; private set; }

    // Catalog kaynagi — henuz gelmediyse null (kismi satir). Name null = "Catalog raporlamadi".
    // Description/Price/Brand eklendi; Price null = fat veri gelmedi (dolu-satir filtresinin isareti).
    // BrandId/CategoryId/Category eklendi — kimlik + ad birlikte tasinir (R7); Id'ler opak degerdir.
    // Event'te kategori zorunludur; buradaki null YALNIZ "Catalog henuz raporlamadi" demektir.
    public string? Name { get; private set; }
    public string? Description { get; private set; }
    public decimal? Price { get; private set; }
    // Brand→çok-yazar (Authors) + tek yayınevi (Publisher). AuthorRef read-model'in KENDİ küçük
    // record'u (BC izolasyonu — Shared tipini sızdırmaz, aynı kavram farklı model). null = Catalog raporlamadı.
    public List<AuthorRef> Authors { get; private set; } = [];
    public Guid? PublisherId { get; private set; }
    public string? Publisher { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string? Category { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool IsDeleted { get; private set; }

    // kanonik ozellikler (ad ciftleri — facet + detay) ve sorgu anahtarlari ("Attribute|Option").
    // SpecKeys, Specs'ten TURETILIR (ApplyCatalog) — jsonb ?| kesisim sorgusunun duz anahtari (R6).
    public List<SpecPair> Specs { get; private set; } = [];
    public string[] SpecKeys { get; private set; } = [];

    // varyant ailesi kodu (opsiyonel; null = ailesiz). Liste gruplama anahtarı
    // coalesce(FamilyCode, ProductId) sorgu-zamanında kurulur (ayrı bayrak saklanmaz).
    public string? FamilyCode { get; private set; }

    // Stock kaynagi — null = henuz raporlamadi ("bilinmiyor"). In-stock bu adetten turetilir.
    public int? StockQuantity { get; private set; }

    // Reviews kaynagi — ReviewSummaryChanged MUTLAK ozeti yazar. null/0 = rozet cizilmez (FR-006).
    public decimal? RatingAverage { get; private set; }
    public int RatingCount { get; private set; }

    // Ayri surec (BackgroundService vb.) sahiplenir; ingestion ASLA yazmaz. Default false.
    public bool IsAvailableForSale { get; private set; }

    // Discount kaynağı (push) — kitabın tek indiriminin yüzdesi + penceresi. null = indirim yok.
    // Etkin fiyat BURADA saklanmaz; sorgu-zamanı view-guard (now pencere içinde mi) + liste fiyatından
    // hesaplanır (StorefrontSellableSchema). Discount.Api fiyat tutmadığından liste değişimi otomatik doğru.
    public int? DiscountPct { get; private set; }
    public DateTime? DiscountStartsAt { get; private set; }
    public DateTime? DiscountEndsAt { get; private set; }

    public static StorefrontView Create(Guid productId) =>
        new() { ProductId = productId };

    public void ApplyCatalog(string name, string description, decimal price,
        IReadOnlyList<AuthorRef> authors, Guid publisherId, string publisher,
        Guid categoryId, string category,
        string? imageUrl, bool isDeleted, IReadOnlyList<SpecPair>? specs = null,
        string? familyCode = null)
    {
        Name = name;
        Description = description;
        Price = price;
        Authors = authors.ToList();
        PublisherId = publisherId;
        Publisher = publisher;
        CategoryId = categoryId;
        Category = category;
        ImageUrl = imageUrl;
        IsDeleted = isDeleted;
        Specs = specs?.ToList() ?? [];
        SpecKeys = Specs.Select(s => s.Key).ToArray();
        // null gelirse aileden çıkar (aile üyeliği Catalog'dan akan güncel değerdir).
        FamilyCode = string.IsNullOrWhiteSpace(familyCode) ? null : familyCode;
    }

    public void ApplyStock(int quantity) => StockQuantity = quantity;

    // Count=0 ⇒ ikisi de temizlenir (kontrat: Average yok sayilir; tek yorum gizlenince rozet kalkar).
    public void ApplyReviewSummary(decimal average, int count)
    {
        RatingAverage = count == 0 ? null : average;
        RatingCount = count;
    }

    // Discount push'u uygular. pct<=0 = temizlik (bitiş/iptal) → alanlar null (indirim kalkar);
    // 1-99 = kitabın indirimi + penceresi. Etkin fiyat sorgu-zamanı view-guard'la hesaplanır (burada değil).
    public void ApplyDiscount(int pct, DateTime? startsAt, DateTime? endsAt)
    {
        if (pct <= 0)
        {
            DiscountPct = null;
            DiscountStartsAt = null;
            DiscountEndsAt = null;
            return;
        }

        DiscountPct = pct;
        DiscountStartsAt = startsAt;
        DiscountEndsAt = endsAt;
    }

    /// <summary>
    /// yeniden-embedding karari (saf; handler + backfill AYNI karara uyar). Bos aciklama → Clear;
    /// metin degisti (Ordinal) YA DA temsil eksik → Generate; aksi halde Keep (API cagrisi bosa gitmez).
    /// Temsil AYRI dokumanda yasar (ProductDescriptionEmbedding) — view satiri sismez.
    /// </summary>
    public static EmbeddingDecision DecideEmbedding(string? newDescription, string? oldDescription, bool hasEmbedding)
    {
        if (string.IsNullOrWhiteSpace(newDescription))
            return EmbeddingDecision.Clear;

        if (!string.Equals(newDescription, oldDescription, StringComparison.Ordinal))
            return EmbeddingDecision.Generate;

        return hasEmbedding ? EmbeddingDecision.Keep : EmbeddingDecision.Generate;
    }
}

// yeniden-embedding karari (enum aggregate/read-model dosyasinda yasar — konvansiyon).
public enum EmbeddingDecision
{
    Keep,
    Generate,
    Clear
}

// read-model'in KENDİ yazar record'u (Id+ad). Shared.IntegrationEvents.AuthorRef'ten AYRI —
// BC izolasyonu (aynı kavram farklı model); event tipini read-model'e sızdırmaz.
public record AuthorRef(Guid Id, string Name);

// satirdaki tek ozellik cifti (kanonik ADlar — event sozlesmesi). Key = "Attribute|Option".
public record SpecPair
{
    public string Attribute { get; private init; } = default!;
    public string Option { get; private init; } = default!;

    public string Key => $"{Attribute}|{Option}";

    private SpecPair() { }

    public static SpecPair Create(string attribute, string option) =>
        new() { Attribute = attribute, Option = option };
}