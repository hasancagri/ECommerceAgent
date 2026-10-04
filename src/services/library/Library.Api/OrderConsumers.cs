namespace Library.Api;

// 086: Order 'OrderCompleted' → kişisel satın-alma kaydı (UserPurchase). Storefront'tan TAŞINDI (FR-011).
// Kalem başına idempotent upsert (Id = "{userId}:{productId}"; tekrar teslim/alım aynı satır).
// Wolverine *Consumers (çoğul) adını keşfetMEZ → Program.cs IncludeType ZORUNLU.
public class OrderConsumers
{
    public async Task Handle(IntegrationEvents.OrderCompleted evt, IDocumentSession session, CancellationToken ct)
    {
        foreach (var item in evt.Items)
            session.Store(Domains.UserPurchase.UserPurchase.Create(evt.UserId, item.ProductId));

        await session.SaveChangesAsync(ct);
    }
}