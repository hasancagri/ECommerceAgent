using OrderAggregate = Order.Api.Domains.Orders.Order;

namespace Order.Api;

// 092 checkout koreografisi: Stock BC'nin StockCommitted/StockCommitFailed event'lerini tüketir.
// StockCommitted → Order.Confirm() + OrderCompleted fanout (Reviews + Library + Basket). Confirm pivot
// mantığı eski Saga/CheckoutConsumers'tan buraya taşındı. StockCommitFailed → Order.Cancel(reason) + manuel
// iade uyarısı (ödeme ÖNCEDEN çekilir — 077; otomatik iade 092 kapsamı DIŞI). Idempotent: Confirm/Cancel
// yalnız Pending'den (çift event güvenli). Süreç güdümlü → Domains/ dışı kök; ad = kaynak BC + Consumers (Stock).
public class StockConsumers
{
    [Transactional]
    public async Task Handle(IntegrationEvents.StockCommitted evt, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        var order = await session.LoadAsync<OrderAggregate>(evt.OrderId, ct);
        if (order is null || order.Status != OrderStatus.Pending)
            return; // idempotent: yok / zaten Confirmed|Cancelled

        var result = order.Confirm();
        if (!result.IsSuccess)
            return; // Pending→Confirm invariant'ı — beklenmeyen, no-op

        session.Store(order);

        // Pivot: sipariş ödeme onaylı tamamlandı → OrderCompleted fanout (Reviews satın-alma kanıtı,
        // Library satın-alma geçmişi, Basket sepet temizliği).
        var items = order.OrderItems
            .Select(oi => new IntegrationEvents.OrderCompletedItem(oi.ProductId, oi.Quantity, oi.UnitPrice))
            .ToList();
        await bus.PublishAsync(new IntegrationEvents.OrderCompleted(
            order.Id, order.BuyerId, new DateTimeOffset(order.CreatedTime, TimeSpan.Zero), items));
    }

    [Transactional]
    public async Task Handle(IntegrationEvents.StockCommitFailed evt, IDocumentSession session, ILogger<StockConsumers> logger, CancellationToken ct)
    {
        var order = await session.LoadAsync<OrderAggregate>(evt.OrderId, ct);
        if (order is null || order.Status != OrderStatus.Pending)
            return; // idempotent: yalnız Pending'den iptal

        var result = order.Cancel(evt.ReasonCode);
        if (!result.IsSuccess)
            return;

        session.Store(order);

        logger.LogWarning(
            "Order {OrderId}: stok yetersiz ({ReasonCode}) → sipariş iptal edildi. Ödeme ÇEKİLMİŞTİ; otomatik iade 092 kapsamı dışı → manuel iade gerekir.",
            order.Id, evt.ReasonCode);
    }
}
