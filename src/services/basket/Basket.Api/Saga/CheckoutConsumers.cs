using static Shared.CheckoutMessages;
using BasketAggregate = Basket.Api.Domains.Baskets.Basket;

namespace Basket.Api.Saga;

// 028/049: checkout sağasının pivot-sonrası adımı — onaylanan siparişin kullanıcısının sepetini siler.
// Basket aggregate'ine doğrudan dokunur (Order/Stock Saga/CheckoutConsumers emsali — ara
// Domains/Features/Commands katmanı yok; TEK çağıranı bu sınıf olduğu için 074'te birleştirildi; eski
// "legacy gRPC" ikinci çağıranı da 074'te ölü kod olduğu doğrulanıp silindi — BasketClearGrpcService).
// İdempotent: sepet yoksa da Ok (FR-010). Pivot sonrası geç adım — başarısızlık siparişi iptal etmez
// (orchestrator retry/log).
// bu BC checkout sağasının KATILIMCISI (kullanıcı isteği değil, süreç güdümlü) → Domains/ dışı Saga/.
// Ad = kaynak BC + Consumers (kökteki <BC>Consumers ile aynı desen); kaynak burada Checkout orchestrator.
public class CheckoutConsumers
{
    [Transactional]
    public async Task<BasketCleared> Handle(
        ClearBasketCommand cmd,
        IDocumentSession session,
        ILogger<CheckoutConsumers> logger,
        CancellationToken ct)
    {
        var basket = await session.Query<BasketAggregate>().FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
        if (basket is null)
            return new BasketCleared(cmd.CheckoutId, true, ErrorClass.None);

        session.Delete(basket);

        logger.LogInformation(
            "Checkout {CheckoutId}: kullanici {UserId} sepeti temizlendi (saga adimi).",
            cmd.CheckoutId, cmd.UserId);

        return new BasketCleared(cmd.CheckoutId, true, ErrorClass.None);
    }
}