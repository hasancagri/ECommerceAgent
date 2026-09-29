namespace Reviews.Api.Domains.Reviews;

// satın-alma kanıtı read-model (gRPC OrderPurchase yerine). Order 'OrderCompleted' fanout'undan
// beslenir; review eligibility eventual-consistency toleranslı (yorum sonra yapılır, anlık-tutarlılık
// gerekmez → gRPC yerine event-fed projeksiyon). AggregateRoot DEĞİL (read-model). Id = "{userId:N}:{productId:N}"
// → idempotent upsert (aynı ürünü tekrar alım aynı satır). Confirmed terminal (void/refund yok) → append-only,
// revoke YOK. Eligibility = LoadAsync(KeyFor(...)) PK lookup.
public class PurchasedProduct
{
    public string Id { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public Guid ProductId { get; private set; }

    private PurchasedProduct()
    {
    }

    public static string KeyFor(Guid userId, Guid productId) => $"{userId:N}:{productId:N}";

    public static PurchasedProduct Create(Guid userId, Guid productId) => new()
    {
        Id = KeyFor(userId, productId),
        UserId = userId,
        ProductId = productId,
    };
}
