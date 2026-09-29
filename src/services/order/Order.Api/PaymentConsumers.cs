using static Shared.CheckoutMessages;
using OrderAggregate = Order.Api.Domains.Orders.Order;

namespace Order.Api;

// hosted-CF ödeme sonucu (Payment.Api fanout) → checkout tetikler / sipariş iptal eder. Süreç-güdümlü
// (kullanıcı tetiklemez) → Domains/ dışı. Ödeme PIVOT'u callback anında (saga dışında) geçildi:
// PaymentSucceeded → StartCheckout (CommitStock→Confirm→ClearBasket; charge YOK); PaymentFailed → Cancel
// (stok hiç düşmedi → saga'ya girmeden). CheckoutId = OrderId (çift event → tek saga, idempotent).
// Ad = kaynak BC + Consumers, "Event" infix'siz (yeni kural; eski ad PaymentEventConsumers).
public class PaymentConsumers
{
    public async Task Handle(IntegrationEvents.PaymentSucceeded evt, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        var order = await session.LoadAsync<OrderAggregate>(evt.OrderId, ct);
        if (order is null || order.Status != OrderStatus.Pending)
            return; // idempotent: yok / zaten işlenmiş

        var items = order.OrderItems
            .Select(oi => new CheckoutItem(oi.ProductId, oi.Quantity, oi.ProductName, oi.UnitPrice))
            .ToList();
        var address = new OrderAddress(
            order.Address.Province, order.Address.District, order.Address.Street,
            order.Address.ZipCode, order.Address.Line);

        await bus.PublishAsync(new StartCheckout(
            CheckoutId: order.Id, UserId: order.BuyerId, Items: items,
            Amount: order.TotalPrice, Address: address, OrderId: order.Id));
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
