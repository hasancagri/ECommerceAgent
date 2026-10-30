using OrderAggregate = Order.Api.Domains.Orders.Order;

namespace Order.Api;

// hosted-CF ödeme sonucu (Payment.Api fanout) → siparişi onaylar / iptal eder.
// Süreç-güdümlü (kullanıcı tetiklemez) → Domains/ dışı. Dijital (PDF) pivot: Stock BC söküldü → stok
// commit ayağı KALKTI. PaymentSucceeded → doğrudan Order.Confirm() + OrderCompleted fanout (Reviews +
// Library + Basket); PaymentFailed → Cancel. Korelasyon doğrudan OrderId. Ad = kaynak BC + Consumers (Payment).
public class PaymentConsumers
{
    [Transactional]
    public async Task Handle(IntegrationEvents.PaymentSucceeded evt, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        var order = await session.LoadAsync<OrderAggregate>(evt.OrderId, ct);
        if (order is null || order.Status != OrderStatus.Pending)
            return; // idempotent: yok / zaten Confirmed|Cancelled

        var result = order.Confirm();
        if (!result.IsSuccess)
            return; // Pending→Confirm invariant'ı — beklenmeyen, no-op

        session.Store(order);

        // Pivot: ödeme onaylı → sipariş tamamlandı → OrderCompleted fanout (Reviews satın-alma kanıtı,
        // Library satın-alma geçmişi + dijital teslim yetkisi, Basket sepet temizliği).
        var items = order.OrderItems
            .Select(oi => new IntegrationEvents.OrderCompletedItem(oi.ProductId, oi.Quantity, oi.UnitPrice))
            .ToList();
        await bus.PublishAsync(new IntegrationEvents.OrderCompleted(
            order.Id, order.BuyerId, new DateTimeOffset(order.CreatedTime, TimeSpan.Zero), items));
    }

    [Transactional]
    public async Task Handle(IntegrationEvents.PaymentFailed evt, IDocumentSession session, CancellationToken ct)
    {
        var order = await session.LoadAsync<OrderAggregate>(evt.OrderId, ct);
        if (order is null || order.Status != OrderStatus.Pending)
            return; // idempotent: yalnız Pending'den iptal

        var result = order.Cancel(evt.ReasonCode);
        if (result.IsSuccess) session.Store(order);
    }
}
