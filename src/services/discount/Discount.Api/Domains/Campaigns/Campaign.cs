namespace Discount.Api.Domains.Campaigns;

/// <summary>
/// Kampanya — Discount BC'nin kök aggregate'i (003, dönüş; tarihçe 079). TEK ürüne yüzde indirim +
/// zaman penceresi. İndirim AYRI kavramdır: ürünün liste fiyatını EZMEZ (fiyat Catalog'da değişmez;
/// indirimli fiyat okuyanda hesaplanır). Bir üründe aynı anda en fazla bir ETKİN indirim —
/// son-gelen-kazanır kararı aggregate'te değil seçim helper'ındadır (CampaignSelection; aggregate
/// diğer kampanyaları bilmez). Zaman DIŞARIDAN verilir (IsActiveAt) — domain saat okumaz.
/// </summary>
public class Campaign : AggregateRoot
{
    public Guid ProductId { get; private set; }
    // 0.01-0.90 (oran; %90 üstü tam-bedava/negatif kazalarına guard).
    public decimal Rate { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public bool IsCancelled { get; private set; }

    private Campaign()
    {
    }

    /// <summary>Kampanya tanımlar. Guard'lar: ürün boş olamaz, oran %1-%90, bitiş > başlangıç.</summary>
    public static ResultDomain<Campaign> Create(Guid productId, decimal rate,
        DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (productId == Guid.Empty)
            return ResultDomain<Campaign>.Error(new MessageItem
            { Property = nameof(ProductId), Code = DiscountResourceConstants.CAMPAIGN_PRODUCT_REQUIRED });
        if (rate is <= 0 or > 0.90m)
            return ResultDomain<Campaign>.Error(new MessageItem
            { Property = nameof(Rate), Code = DiscountResourceConstants.CAMPAIGN_RATE_INVALID });
        if (endsAt <= startsAt)
            return ResultDomain<Campaign>.Error(new MessageItem
            { Property = nameof(EndsAt), Code = DiscountResourceConstants.CAMPAIGN_WINDOW_INVALID });

        return ResultDomain<Campaign>.Ok(new Campaign
        {
            ProductId = productId,
            Rate = rate,
            StartsAt = startsAt,
            EndsAt = endsAt,
        });
    }

    /// <summary>Kampanyayı iptal eder (pencere bitmeden etkinliği düşer). İdempotent — hep Ok;
    /// kayıt silinmez (durum değişimi, geçmiş raporlama için durur).</summary>
    public ResultDomain Cancel()
    {
        IsCancelled = true;
        return ResultDomain.Ok();
    }

    /// <summary>Verilen anda etkin mi — saf karar: pencere içinde VE iptal edilmemiş.</summary>
    public bool IsActiveAt(DateTimeOffset now) =>
        !IsCancelled && now >= StartsAt && now < EndsAt;
}

/// <summary>Ürün-başına etkin oran seçimi — saf helper; CampaignWindowProcess bunu çağırır.
/// Çakışan etkin kampanyalarda SON OLUŞTURULAN kazanır (079 kuralı).</summary>
public static class CampaignSelection
{
    public static decimal? ResolveActiveRate(IEnumerable<Campaign> campaigns, DateTimeOffset now) =>
        campaigns
            .Where(c => c.IsActiveAt(now))
            .OrderByDescending(c => c.CreatedTime)
            .Select(c => (decimal?)c.Rate)
            .FirstOrDefault();
}
