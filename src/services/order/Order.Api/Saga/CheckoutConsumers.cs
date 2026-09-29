using static Shared.CheckoutMessages;
using OrderAggregate = Order.Api.Domains.Orders.Order;

namespace Order.Api.Saga;

// checkout orchestrator sipariş broker handler'ları. Komutları OrderCommandsQueue'dan tüketir,
// Order aggregate davranışını (Create/Confirm/Cancel — İlke II) tetikler, sonucu reply kuyruğuna
// yayınlar. Pivot (Confirm) anında OrderCompleted fanout'u (048 Personalization) burada yayılır.
// Idempotent: PaymentId=CheckoutId ile tek sipariş; Confirm/Cancel yalnız Pending'den.
// bu BC checkout sağasının KATILIMCISI (kullanıcı isteği değil, süreç güdümlü) → Domains/ dışı Saga/.
// Ad = kaynak BC/orkestratör + Consumers (Stock.Api Saga/CheckoutConsumers ile aynı desen); kaynak
// burada Checkout orchestrator (eski ad OrderEventHandlers — aggregate'e göre, yeni kural kaynağa göre).
public class CheckoutConsumers
{
    [Transactional]
    public async Task<OrderCreated> Handle(CreateOrderCommand cmd, IDocumentSession session, CancellationToken ct)
    {
        var existing = await session.Query<OrderAggregate>().FirstOrDefaultAsync(o => o.PaymentId == cmd.CheckoutId, ct);
        if (existing is not null)
            return new OrderCreated(cmd.CheckoutId, existing.Id, true, ErrorClass.None);

        var address = new Address(cmd.Address.Province, cmd.Address.District, cmd.Address.Street, cmd.Address.ZipCode, cmd.Address.Line);
        var order = OrderAggregate.Create(cmd.UserId, address, cmd.CheckoutId);
        foreach (var item in cmd.Items)
        {
            var add = order.AddOrderItem(item.ProductId, item.Name, item.UnitPrice, item.Quantity);
            if (!add.IsSuccess)
                return new OrderCreated(cmd.CheckoutId, Guid.Empty, false, ErrorClass.Permanent, add.Messages.FirstOrDefault()?.Code);
        }

        session.Store(order);
        return new OrderCreated(cmd.CheckoutId, order.Id, true, ErrorClass.None);
    }

    [Transactional]
    public async Task<OrderConfirmed> Handle(ConfirmOrderCommand cmd, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        var order = await session.LoadAsync<OrderAggregate>(cmd.OrderId, ct);
        if (order is null)
            return new OrderConfirmed(cmd.CheckoutId, false, ErrorClass.Permanent, OrderResourceConstants.ORDER_INVALID_STATUS_TRANSITION);

        if (order.Status == OrderStatus.Confirmed)
            return new OrderConfirmed(cmd.CheckoutId, true, ErrorClass.None); // idempotent — tekrar yayınlama yok

        var result = order.Confirm();
        if (!result.IsSuccess)
            return new OrderConfirmed(cmd.CheckoutId, false, ErrorClass.Permanent, result.Messages.FirstOrDefault()?.Code);

        session.Store(order);

        // Pivot: sipariş ödeme onaylı tamamlandı → Personalization satın-alma sinyali (048, fanout).
        var items = order.OrderItems
            .Select(oi => new IntegrationEvents.OrderCompletedItem(oi.ProductId, oi.Quantity, oi.UnitPrice))
            .ToList();
        await bus.PublishAsync(new IntegrationEvents.OrderCompleted(
            order.Id, order.BuyerId, new DateTimeOffset(order.CreatedTime, TimeSpan.Zero), items));

        return new OrderConfirmed(cmd.CheckoutId, true, ErrorClass.None);
    }

    [Transactional]
    public async Task<OrderCancelled> Handle(CancelOrderCommand cmd, IDocumentSession session, CancellationToken ct)
    {
        var order = await session.LoadAsync<OrderAggregate>(cmd.OrderId, ct);
        if (order is null || order.Status == OrderStatus.Cancelled)
            return new OrderCancelled(cmd.CheckoutId, true, ErrorClass.None); // idempotent

        var result = order.Cancel(cmd.ReasonCode);
        if (result.IsSuccess) session.Store(order);
        return new OrderCancelled(cmd.CheckoutId, true, ErrorClass.None);
    }
}
