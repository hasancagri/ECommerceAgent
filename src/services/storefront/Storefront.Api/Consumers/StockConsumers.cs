namespace Storefront.Api.Consumers;

// 086: Stock `StockChangedEvent` → ürün stream'ine append (fold/ES projeksiyonun işi).
public static class StockConsumers
{
    public static async Task Handle(IntegrationEvents.StockChangedEvent evt, IDocumentSession session, CancellationToken ct)
    {
        session.Events.Append(evt.ProductId, evt);
        await session.SaveChangesAsync(ct);
    }
}