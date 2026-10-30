using Discount.Api.Domains.ProductDiscounts;

namespace Discount.Api.Domains.Campaigns;

// kampanya aktifleştirme + temizleme çekirdeği — HEM kullanıcı slice'ı (CreateCampaign) HEM iç süreç
// (Process/CampaignScheduleHandler) çağırır. Aggregate değil, ikisinin paylaştığı süreç-glue helper'ı
// (conventions: ortak saf-olmayan altyapı Domains'te kalabilir). ProductDiscount store/temizle burada
// tek yerde — SON-GELEN-KAZANIR (overwrite) + expiry aynı mantık. Dışa fanout YOK (vitrin event'i söküldü).
public static class CampaignApplication
{
    // Aktifleştir: süzgeç → kitap seti → her kitaba ProductDiscount YAZ (varsa ÜZERİNE yaz — son-gelen-
    // kazanır, atlama yok; PK=ProductId Marten upsert'i overwrite eder). Etkin yüzde checkout gRPC'sinden
    // okunur. İdempotent: tekrar çalışırsa aynı değeri yeniden yazar (etki aynı).
    public static async Task<(int Applied, int Skipped)> ActivateAsync(
        Campaign campaign, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        var candidates = await CampaignSelectionResolver.ResolveAsync(
            campaign.ScopeType, campaign.ScopeRef, session, ct);
        if (candidates.Count == 0)
            return (0, 0);

        foreach (var productId in candidates)
        {
            session.Store(ProductDiscount.Create(
                productId, campaign.Id, campaign.Percentage, campaign.StartsAt, campaign.EndsAt));
            // ProductDiscountChanged vitrin event'i SÖKÜLDÜ (tek tüketici Storefront'tu). İndirim
            // Order'a checkout gRPC'siyle (DiscountRead) akar; ProductDiscount store'u o sorgunun kaynağı.
        }

        return (candidates.Count, 0);
    }

    // Temizle (bitiş/iptal): campaignId'ye ait ProductDiscount'ları sil (checkout gRPC sorgusu liste
    // fiyatına döner, dışa fanout YOK). İdempotent: satır yoksa no-op (bayat/çift fire güvenli).
    public static async Task ClearAsync(
        Guid campaignId, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        var discounts = await session.Query<ProductDiscount>()
            .Where(x => x.CampaignId == campaignId)
            .ToListAsync(ct);

        foreach (var discount in discounts)
        {
            session.Delete(discount);
            // ProductDiscountChanged(pct:0) temizlik event'i SÖKÜLDÜ (tek tüketici Storefront'tu).
            // ProductDiscount silinmesi checkout gRPC sorgusunu doğrudan liste fiyatına döndürür.
        }
    }
}
