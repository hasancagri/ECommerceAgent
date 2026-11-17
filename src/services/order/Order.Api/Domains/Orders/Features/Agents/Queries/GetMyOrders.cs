using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace Order.Api.Domains.Orders.Features.Agents.Queries;

// 007 US2: kullanıcının siparişleri. Scope: order.read. YALNIZ çağıranın (BuyerId token'dan).
// Opsiyonel durum filtresi (DEFAULT null — optional-param tuzağı).
public static class GetMyOrders
{
    public record Query(Guid UserId, string? Status);

    public class OrderItem
    {
        public string OrderNo { get; set; } = default!;
        public string Status { get; set; } = default!;
        public DateTime PlacedAt { get; set; }
        public decimal Total { get; set; }
        public int LineCount { get; set; }
    }

    public class Response
    {
        public IReadOnlyList<OrderItem> Orders { get; set; } = [];
    }

    public class GetMyOrdersHandler(IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Query query, CancellationToken ct)
        {
            if (query.UserId == Guid.Empty)
                return FeatureObjectResultModel<Response>.Ok(new Response());

            var q = session.Query<global::Order.Api.Domains.Orders.Order>().Where(o => o.BuyerId == query.UserId);

            if (!string.IsNullOrWhiteSpace(query.Status)
                && Enum.TryParse<OrderStatus>(query.Status, ignoreCase: true, out var status))
            {
                q = q.Where(o => o.Status == status);
            }

            var rows = await q.ToListAsync(ct);
            var orders = rows.Select(o => new OrderItem
            {
                OrderNo = o.Code,
                Status = o.Status.ToString(),
                PlacedAt = o.CreatedTime,
                Total = o.TotalPrice,
                LineCount = o.OrderItems.Count,
            }).ToList();

            return FeatureObjectResultModel<Response>.Ok(new Response { Orders = orders });
        }
    }

    [McpServerToolType]
    public static class Tool
    {
        [McpServerTool(Name = "get_my_orders")]
        [Description("Giriş yapmış kullanıcının siparişlerini döner (sipariş no, durum, tarih, tutar, " +
                     "kalem sayısı). Opsiyonel durum filtresi (Pending/Confirmed/Cancelled). YALNIZ çağıranın.")]
        public static async Task<Response> GetMyOrders(
            IMessageBus bus, ICurrentUser currentUser, IHttpContextAccessor http,
            [Description("Opsiyonel durum filtresi: Pending, Confirmed, Cancelled")] string? status = null,
            CancellationToken cancellationToken = default)
        {
            currentUser.Load(http.HttpContext!.User);
            var result = await bus.InvokeAsync<FeatureObjectResultModel<Response>>(
                new Query(currentUser.Id, status), cancellationToken);
            return result.Data ?? new Response();
        }
    }
}
