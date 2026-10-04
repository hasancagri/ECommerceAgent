namespace Storefront.Api.Search;

// 086: Elasticsearch arama dokümanının SAF fold durumu — StorefrontView'un (Postgres read-model)
// yerini alır. Rich aggregate DEĞİL (invariant taşımaz); ürün stream'indeki yabancı integration
// event'lerinin (Catalog/Stock/Reviews/Discount) katlanmış birleşimi. Marten `AggregateStreamAsync`
// bunu üretir (konvansiyon: `Apply(TEvent)`), projeksiyon ES'e yazar/siler. Her kaynak YALNIZ kendi
// alan grubunu yazar; kısmi satır geçerli; mutlak değer son-yazan-kazanır (R3).
//
// added_at + embedding fold'un DIŞINDADIR (yan etki / zaman) — projeksiyon yönetir (ilk görülme
// yaklaşık, embedding OpenAI çağrısı). Burada yalnız saf, test edilebilir alan katlama + satılabilirlik
// + etkin fiyat + yeniden-embedding kararı yaşar (İLKE VI).
public class StorefrontDocument
{
    // Marten stream id = ProductId (ES _id ile birebir). public set: Marten aggregation yazar.
    public Guid Id { get; set; }

    // Catalog kaynağı — null = Catalog henüz raporlamadı (kısmi satır).
    public string? Name { get; private set; }
    public string? Description { get; private set; }
    public decimal? Price { get; private set; }
    public List<string> Authors { get; private set; } = [];
    public string? Publisher { get; private set; }
    public string? Category { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool IsDeleted { get; private set; }
    public List<SpecPair> Specs { get; private set; } = [];
    public string? FamilyCode { get; private set; }

    // Stock kaynağı — null = henüz raporlamadı ("bilinmiyor").
    public int? Stock { get; private set; }

    // Reviews kaynağı — MUTLAK özet; Count=0 temizler (rozet çizilmez).
    public decimal? RatingAverage { get; private set; }
    public int RatingCount { get; private set; }

    // Discount kaynağı — kitabın tek indiriminin yüzdesi + penceresi. null = indirim yok.
    public int? DiscountPct { get; private set; }
    public DateTime? DiscountStartsAt { get; private set; }
    public DateTime? DiscountEndsAt { get; private set; }

    public StorefrontDocument()
    {
    }

    // ── Marten aggregation konvansiyonu (Apply(TEvent)) → domain-adlı saf fold'a delege ──
    public void Apply(IntegrationEvents.ProductChangedEvent e) => ApplyCatalog(e);
    public void Apply(IntegrationEvents.StockChangedEvent e) => ApplyStock(e);
    public void Apply(IntegrationEvents.ReviewSummaryChanged e) => ApplyReviewSummary(e);
    public void Apply(IntegrationEvents.ProductDiscountChanged e) => ApplyDiscount(e);

    public void ApplyCatalog(IntegrationEvents.ProductChangedEvent e)
    {
        Id = e.ProductId;
        Name = e.Name;
        Description = e.Description;
        Price = e.Price;
        // event yazar çiftlerinden yalnız ad (ES authors text alanı; Id sorguya gerekmez).
        Authors = (e.Authors ?? []).Select(a => a.Name).ToList();
        Publisher = e.Publisher;
        Category = e.Category;
        ImageUrl = e.ImageUrl;
        IsDeleted = e.IsDeleted;
        Specs = (e.Specs ?? []).Select(s => SpecPair.Create(s.Attribute, s.Option)).ToList();
        // null gelirse aileden çıkar (aile üyeliği Catalog'dan akan güncel değerdir).
        FamilyCode = string.IsNullOrWhiteSpace(e.FamilyCode) ? null : e.FamilyCode;
    }

    public void ApplyStock(IntegrationEvents.StockChangedEvent e)
    {
        Id = e.ProductId;
        Stock = e.Quantity;
    }

    // Count=0 ⇒ ikisi de temizlenir (kontrat: Average yok sayılır; rozet kalkar).
    public void ApplyReviewSummary(IntegrationEvents.ReviewSummaryChanged e)
    {
        Id = e.ProductId;
        RatingAverage = e.Count == 0 ? null : e.Average;
        RatingCount = e.Count;
    }

    // pct<=0 = temizlik (bitiş/iptal) → alanlar null; 1-99 = kitabın indirimi + penceresi.
    public void ApplyDiscount(IntegrationEvents.ProductDiscountChanged e)
    {
        Id = e.ProductId;
        if (e.DiscountPct <= 0)
        {
            DiscountPct = null;
            DiscountStartsAt = null;
            DiscountEndsAt = null;
            return;
        }

        DiscountPct = e.DiscountPct;
        DiscountStartsAt = e.StartsAt;
        DiscountEndsAt = e.EndsAt;
    }

    // Satılabilirlik (FR-008): doc ES'e YALNIZ satılabilirse yazılır. IsDeleted ya da Price yok
    // (Catalog raporlamadı) → projeksiyon doc'u SİLER — satılamaz kitap index'te hiç bulunmaz.
    public bool IsSellable => !IsDeleted && Price is not null;

    // Etkin fiyat YAZIM anında hesaplanır (SQL view-guard'ın yerini alır). İndirim penceresi
    // içindeyse indirimli, değilse liste fiyatı. null Price → null (satılamaz zaten silinir).
    public decimal? EffectivePrice(DateTimeOffset now)
    {
        if (Price is null)
            return null;

        if (DiscountPct is int pct && pct > 0
            && (DiscountStartsAt is null || now >= DiscountStartsAt)
            && (DiscountEndsAt is null || now < DiscountEndsAt))
            return Math.Round(Price.Value * (1 - pct / 100m), 2);

        return Price.Value;
    }

    // İndirim şu an aktif mi (effective_price ile aynı pencere mantığı; projeksiyon discount_pct/
    // discount_ends_at alanlarını yalnız aktifse yazsın diye).
    public bool IsDiscountActive(DateTimeOffset now) =>
        DiscountPct is int pct && pct > 0
        && (DiscountStartsAt is null || now >= DiscountStartsAt)
        && (DiscountEndsAt is null || now < DiscountEndsAt);

    /// <summary>
    /// Yeniden-embedding kararı (saf). Boş açıklama → Clear; metin değişti (Ordinal) YA DA temsil
    /// eksik → Generate; aksi halde Keep (API çağrısı boşa gitmez). Önceki açıklama/vektör ES'teki
    /// mevcut doc'tan okunur (projeksiyon). StorefrontView.DecideEmbedding'ten taşındı (067 testi).
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

// yeniden-embedding kararı (enum doküman dosyasında — konvansiyon).
public enum EmbeddingDecision
{
    Keep,
    Generate,
    Clear
}

// satırdaki tek özellik çifti (kanonik ADlar — event sözleşmesi). ES nested {attribute, option}.
public record SpecPair
{
    public string Attribute { get; private init; } = default!;
    public string Option { get; private init; } = default!;

    private SpecPair()
    {
    }

    public static SpecPair Create(string attribute, string option) =>
        new() { Attribute = attribute, Option = option };
}