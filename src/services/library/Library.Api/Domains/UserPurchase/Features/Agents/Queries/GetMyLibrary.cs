using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace Library.Api.Domains.UserPurchase.Features.Agents.Queries;

// 007 US2: kullanıcının kitaplığı (satın aldıkları). UserPurchase yalnız ProductId tutar (künye yok) —
// başlık istenirse model get_book ile zenginleştirir (cross-tool; Library aggregate'e dokunulmaz).
// Scope: library.read (MapMcp üzerinde). YALNIZ çağıranın verisi (UserId token'dan).
public static class GetMyLibrary
{
    public record Query(Guid UserId);

    public class Item
    {
        public Guid ProductId { get; set; }
    }

    public class Response
    {
        public IReadOnlyList<Item> Items { get; set; } = [];
        public int Total { get; set; }
    }

    public class GetMyLibraryHandler(IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Query query, CancellationToken ct)
        {
            if (query.UserId == Guid.Empty)
                return FeatureObjectResultModel<Response>.Ok(new Response());

            var rows = await session.Query<Library.Api.Domains.UserPurchase.UserPurchase>()
                .Where(p => p.UserId == query.UserId)
                .ToListAsync(ct);

            var items = rows.Select(r => new Item { ProductId = r.ProductId }).ToList();
            return FeatureObjectResultModel<Response>.Ok(new Response { Items = items, Total = items.Count });
        }
    }

    [McpServerToolType]
    public static class Tool
    {
        [McpServerTool(Name = "get_my_library")]
        [Description("Giriş yapmış kullanıcının kitaplığındaki (satın aldığı) kitapların kimliklerini döner. " +
                     "Başlık/künye için get_book ile zenginleştir. YALNIZ çağıranın verisi.")]
        public static async Task<Response> GetMyLibrary(
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
