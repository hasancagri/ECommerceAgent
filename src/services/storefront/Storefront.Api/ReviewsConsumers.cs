namespace Storefront.Api;

public static class ReviewsConsumers
{
    // puan ozeti — MUTLAK deger yazilir (Count=0 temizler). Satir yoksa da yaratilir
    // (kismi satir gecerli — Catalog verisi gelince dolu-satir filtresine girer).
    public static async Task Handle(IntegrationEvents.ReviewSummaryChanged evt, IDocumentSession session, CancellationToken ct)
    {
        var view = await session.LoadAsync<StorefrontView>(evt.ProductId, ct)
                   ?? StorefrontView.Create(evt.ProductId);

        view.ApplyReviewSummary(evt.Average, evt.Count);

        session.Store(view);
        await session.SaveChangesAsync(ct);
    }
}
