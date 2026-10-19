using BasketAggregate = Basket.Api.Domains.Baskets.Basket;

namespace Basket.Api;

// 092 checkout koreografisi: Order BC'nin OrderCompleted event'ini tüketir — onaylanan siparişin
// kullanıcısının sepetini idempotent siler (sepet yoksa da no-op). Eski Saga/CheckoutConsumers ClearBasket
// adımının yerini alır. Sepet yalnız sipariş Confirmed olunca (OrderCompleted pivot sonrası) silinir.
// Süreç güdümlü (kullanıcı tetiklemez) → Domains/ dışı kök; ad = kaynak BC + Consumers (Order).
public class OrderConsumers
{
    [Transactional]
    public async Task Handle(
        IntegrationEvents.OrderCompleted evt,
        IDocumentSession session,
        ILogger<OrderConsumers> logger,
        CancellationToken ct)
    {
        var basket = await session.Query<BasketAggregate>().FirstOrDefaultAsync(x => x.UserId == evt.UserId, ct);
        if (basket is null)
            return; // idempotent: sepet yoksa da Ok

        session.Delete(basket);

        logger.LogInformation(
            "Order {OrderId}: kullanici {UserId} sepeti temizlendi (checkout tamamlandi).",
            evt.OrderId, evt.UserId);
    }
}
