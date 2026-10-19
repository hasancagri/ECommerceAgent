using OrderAggregate = Order.Api.Domains.Orders.Order;

namespace Order.Api;

// hosted-CF ödeme sonucu (Payment.Api fanout) → checkout koreografisini tetikler / sipariş iptal eder.
// Süreç-güdümlü (kullanıcı tetiklemez) → Domains/ dışı. Ödeme PIVOT'u callback anında (saga/orchestrator
// yok, 092) geçildi: PaymentSucceeded → StockCommitRequested (Stock atomik commit eder); PaymentFailed →
// Cancel (stok hiç düşmedi). CheckoutId kavramı kalktı — korelasyon doğrudan OrderId.
// Ad = kaynak BC + Consumers (Payment).
public class PaymentConsumers
{
    public async Task Handle(IntegrationEvents.PaymentSucceeded evt, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        var order = await session.LoadAsync<OrderAggregate>(evt.OrderId, ct);
        if (order is null || order.Status != OrderStatus.Pending)
            return; // idempotent: yok / zaten işlenmiş

        var items = order.OrderItems
            .Select(oi => new IntegrationEvents.StockCommitItem(oi.ProductId, oi.Quantity))
            .ToList();

        await bus.PublishAsync(new IntegrationEvents.StockCommitRequested(order.Id, order.BuyerId, items));
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
