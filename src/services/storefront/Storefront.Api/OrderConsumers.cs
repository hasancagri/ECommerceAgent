namespace Storefront.Api;

public static class OrderConsumers
{
    // kişisel feed sinyali — tamamlanan siparişin her kalemi UserPurchase satırına döner.
    // PK = "{userId:N}:{productId:N}" → Store = idempotent upsert (tekrar teslim/tekrar alım zararsız).
    public static async Task Handle(IntegrationEvents.OrderCompleted evt, IDocumentSession session, CancellationToken ct)
    {
        foreach (var item in evt.Items)
            session.Store(Domains.UserPurchase.UserPurchase.Create(evt.UserId, item.ProductId));

        await session.SaveChangesAsync(ct);
    }
}
