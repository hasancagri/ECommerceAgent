namespace Library.Api.Domains.UserPurchase;

// 086: kullanıcı satın-alma birikimi — Storefront'tan Library BC'ye TAŞINDI (FR-011). Kişisel kayıt;
// sorgu/arama yüzeyinin yapısal dışıdır. Order 'OrderCompleted' fanout'undan beslenir. AggregateRoot
// DEĞİL (read-model). Id = "{userId:N}:{productId:N}" → idempotent upsert (aynı ürünü tekrar alım aynı
// satır). Backfill yok; append-only, revoke yok (mevcut kontrat korunur).
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