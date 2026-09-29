namespace Storefront.Api.Consumers;

public static class StockConsumers
{
    public static async Task Handle(IntegrationEvents.StockChangedEvent evt, IDocumentSession session, CancellationToken ct)
    {
        var view = await session.LoadAsync<StorefrontView>(evt.ProductId, ct)
                   ?? StorefrontView.Create(evt.ProductId);

        view.ApplyStock(evt.Quantity);

        session.Store(view);
        await session.SaveChangesAsync(ct);
    }
}
