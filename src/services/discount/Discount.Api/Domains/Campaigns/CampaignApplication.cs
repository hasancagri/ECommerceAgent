using Discount.Api.Domains.ProductDiscounts;

namespace Discount.Api.Domains.Campaigns;

// kampanya aktifleştirme + temizleme çekirdeği — HEM kullanıcı slice'ı (CreateCampaign) HEM iç süreç
// (Process/CampaignScheduleHandler) çağırır. Aggregate değil, ikisinin paylaştığı süreç-glue helper'ı
// (conventions: ortak saf-olmayan altyapı Domains'te kalabilir). ProductDiscount store/temizle + kitap
// başına `ProductDiscountChanged` push burada tek yerde — SON-GELEN-KAZANIR (overwrite) + expiry aynı mantık.
public static class CampaignApplication
{
    // Aktifleştir: süzgeç → kitap seti → her kitaba ProductDiscount YAZ (varsa ÜZERİNE yaz — son-gelen-
    // kazanır, atlama yok; PK=ProductId Marten upsert'i overwrite eder) + ProductDiscountChanged push.
    // İdempotent: tekrar çalışırsa aynı değeri yeniden yazar (etki aynı).
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
            await bus.PublishAsync(new IntegrationEvents.ProductDiscountChanged(
                productId, campaign.Percentage, campaign.StartsAt, campaign.EndsAt));
        }

        return (candidates.Count, 0);
    }

    // Temizle (bitiş/iptal): campaignId'ye ait ProductDiscount'ları sil + her biri için
    // ProductDiscountChanged(pct:0) push (Storefront satırını liste fiyatına döndürür). İdempotent: satır
    // yoksa no-op (bayat/çift fire güvenli).
    public static async Task ClearAsync(
        Guid campaignId, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        var discounts = await session.Query<ProductDiscount>()
            .Where(x => x.CampaignId == campaignId)
            .ToListAsync(ct);

        foreach (var discount in discounts)
        {
            session.Delete(discount);
            await bus.PublishAsync(new IntegrationEvents.ProductDiscountChanged(
                discount.ProductId, 0, null, null));
        }
    }
}
