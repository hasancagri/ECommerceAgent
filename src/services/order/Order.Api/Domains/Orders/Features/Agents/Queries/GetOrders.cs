namespace Order.Api.Domains.Orders.Features.Agents.Queries;

public static class GetOrders
{
    public record GetOrdersQuery(Guid UserId);

    public record GetOrdersResponse
    {
        public Guid Id { get; private set; }
        public string Code { get; private set; } = null!;
        public DateTime CreatedTime { get; private set; }
        public decimal TotalPrice { get; private set; }
        public OrderStatus Status { get; private set; }
        // yalniz Cancelled'da dolu; agent yaniti sebep kodunu aynen tasir.
        public string? CancelReason { get; private set; }
        public List<OrderItemResponse> Items { get; private set; } = [];

        public static GetOrdersResponse From(Order order) => new()
        {
            Id = order.Id,
            Code = order.Code,
            CreatedTime = order.CreatedTime,
            TotalPrice = order.TotalPrice,
            Status = order.Status,
            CancelReason = order.CancelReason,
            Items =
            [
                .. order.OrderItems.Select(i => new OrderItemResponse
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    UnitPrice = i.UnitPrice
                })
            ]
        };
    }

    public record OrderItemResponse
    {
        public Guid ProductId { get; init; }
        public string ProductName { get; init; } = null!;
        public decimal UnitPrice { get; init; }
    }

    public class GetOrdersQueryHandler(IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<List<GetOrdersResponse>>> Handle(
            GetOrdersQuery query,
            CancellationToken ct)
        {
            var orders = await session.Query<Order>()
                .Where(x => x.BuyerId == query.UserId)
                .ToListAsync(ct);

            var response = orders.Select(GetOrdersResponse.From).ToList();
            return FeatureObjectResultModel<List<GetOrdersResponse>>.Ok(response);
        }
    }
}

// place_order (agent charge) SÖKÜLDÜ. 077: hosted-CF start_payment geldi. Agent yüzeyi: sipariş
// listeleme + ödeme başlatma (hosted link).
[McpServerToolType]
public static class GetOrdersMcpTool
{
    [McpServerTool(Name = Shared.OrderTools.GetOrders)]
    [Description(Shared.McpToolDescriptions.OrderTools.GetOrders)]
    public static Task<FeatureObjectResultModel<List<GetOrders.GetOrdersResponse>>> GetOrdersAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<List<GetOrders.GetOrdersResponse>>>(
            new GetOrders.GetOrdersQuery(userId), ct);
    }
}
