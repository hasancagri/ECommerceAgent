namespace Reviews.Api;

// satın-alma kanıtı read-model'i besler (gRPC yerine). OrderCompleted (ödeme onaylı, Confirmed
// terminal) her kalem için PurchasedProduct upsert → eligibility lokal lookup. Idempotent: Id composite
// (aynı user+ürün tekrar → aynı satır). At-least-once güvenli; durable local queue + retry.
[Transactional]
public class OrderConsumers
{
    public async Task Handle(
        IntegrationEvents.OrderCompleted evt,
        IDocumentSession session,
        CancellationToken ct)
    {
        foreach (var item in evt.Items)
            session.Store(PurchasedProduct.Create(evt.UserId, item.ProductId));
    }
}
