namespace Storefront.Api.Domains.UserPurchase;

// kullanıcı satın-alma birikimi — kişisel feed'in tek sinyal kaynağı. Order 'OrderCompleted'
// fanout'undan beslenir (Reviews 'PurchasedProduct' emsalinin Storefront kopyası — bilinçli tekrar,
// BC izolasyonu). AggregateRoot DEĞİL (read-model). Id = "{userId:N}:{productId:N}" → idempotent
// upsert (aynı ürünü tekrar alım aynı satır). Backfill yok; append-only, revoke yok.
public class UserPurchase
{
    public string Id { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public Guid ProductId { get; private set; }

    private UserPurchase()
    {
    }

    public static string KeyFor(Guid userId, Guid productId) => $"{userId:N}:{productId:N}";

    public static UserPurchase Create(Guid userId, Guid productId) => new()
    {
        Id = KeyFor(userId, productId),
        UserId = userId,
        ProductId = productId,
    };
}