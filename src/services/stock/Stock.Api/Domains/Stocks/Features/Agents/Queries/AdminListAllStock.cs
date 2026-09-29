namespace Stock.Api.Domains.Stocks.Features.Agents.Queries;

// admin stok genel görünüm (agent yüzeyi) — GetAllStock query İKİZİ (bilinçli tekrar; agent
// slice Commands/Queries'e IMessageBus ile bile gitmez). Salt-okuma: scope attribute YOK.
// Sayfalama opsiyonel; verilmezse tüm kayıtlar döner.
public static class AdminListAllStock
{
    public record AdminListAllStockQuery(int? Page, int? PageSize);

    public class StockItemResponse
    {
        public Guid ProductId { get; set; }
        public int OnHand { get; set; }

        public static StockItemResponse From(ProductStock stock) => new()
        {
            ProductId = stock.ProductId,
            OnHand = stock.OnHand
        };
    }

    public class AdminListAllStockQueryHandler
    {
        public async Task<FeatureListResultModel<StockItemResponse>> Handle(
            AdminListAllStockQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var q = session.Query<ProductStock>().OrderBy(x => x.ProductId).AsQueryable();

            if (query.Page is int page && query.PageSize is int pageSize && page > 0 && pageSize > 0)
                q = q.Skip((page - 1) * pageSize).Take(pageSize);

            var stocks = await q.ToListAsync(ct);
            var response = stocks.Select(StockItemResponse.From).ToList();
            return FeatureListResultModel<StockItemResponse>.Ok(response);
        }
    }
}

[McpServerToolType]
public static class AdminListAllStockMcpTool
{
    [McpServerTool(Name = Shared.StockAdminTools.ListAllStock)]
    [Description(Shared.McpToolDescriptions.StockAdminTools.ListAllStock)]
    public static Task<FeatureListResultModel<AdminListAllStock.StockItemResponse>> AdminListAllStockAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Sayfa numarasi (1'den baslar); verilmezse sayfalama yok")] int? page = null,
        [Description("Sayfa basina kayit; verilmezse sayfalama yok")] int? pageSize = null)
        => bus.InvokeAsync<FeatureListResultModel<AdminListAllStock.StockItemResponse>>(
            new AdminListAllStock.AdminListAllStockQuery(page, pageSize), ct);
}
