using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace Basket.Api.Domains.Baskets.Features.Agents.Queries;

// 014 US?: giriş yapmış kullanıcının sepeti. Scope: basket.read. YALNIZ çağıranın (UserId token'dan).
// Query-only — yazma yok; sepet yoksa boş Response.
public static class GetBasket
{
    public record Query(Guid UserId);

    public class LineItem
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = default!;
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }

    public class Response
    {
        public IReadOnlyList<LineItem> Items { get; set; } = [];
        public int TotalQuantity { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class GetBasketHandler(IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Query query, CancellationToken ct)
        {
            if (query.UserId == Guid.Empty)
                return FeatureObjectResultModel<Response>.Ok(new Response());

            var basket = await session.Query<Basket>()
                .FirstOrDefaultAsync(b => b.UserId == query.UserId, ct);

            if (basket is null)
                return FeatureObjectResultModel<Response>.Ok(new Response());

            var items = basket.Items.Select(i => new LineItem
            {
                ProductId = i.Id,
                Name = i.Name,
                ImageUrl = i.ImageUrl,
                Price = i.Price,
                Quantity = i.Quantity,
            }).ToList();

            return FeatureObjectResultModel<Response>.Ok(new Response
            {
                Items = items,
                TotalQuantity = items.Sum(x => x.Quantity),
                TotalPrice = basket.GetTotalPrice(),
            });
        }
    }

    [McpServerToolType]
    public static class Tool
    {
        [McpServerTool(Name = "get_basket")]
        [Description("Giriş yapmış kullanıcının sepet kalemlerini döner. YALNIZ çağıranın.")]
        public static async Task<Response> GetBasket(
            IMessageBus bus, ICurrentUser currentUser, IHttpContextAccessor http,
            CancellationToken cancellationToken = default)
        {
            currentUser.Load(http.HttpContext!.User);
            var result = await bus.InvokeAsync<FeatureObjectResultModel<Response>>(
                new Query(currentUser.Id), cancellationToken);
            return result.Data ?? new Response();
        }
    }
}
