namespace Storefront.Api.Consumers;

// 086: Reviews `ReviewSummaryChanged` → ürün stream'ine append. MUTLAK özet; fold Count=0'ı temizler.
public static class ReviewsConsumers
{
    public static async Task Handle(IntegrationEvents.ReviewSummaryChanged evt, IDocumentSession session, CancellationToken ct)
    {
        session.Events.Append(evt.ProductId, evt);
        await session.SaveChangesAsync(ct);
    }
}