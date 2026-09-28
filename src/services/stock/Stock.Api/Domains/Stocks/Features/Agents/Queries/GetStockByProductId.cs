namespace Stock.Api.Domains.Stocks.Features.Agents.Queries;

public static class GetStockByProductId
{
    public record GetStockByProductIdQuery(Guid ProductId);

    public class GetStockResponse
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }

        public static GetStockResponse From(ProductStock stock) => new()
        {
            ProductId = stock.ProductId,
            Quantity = stock.Quantity
        };
    }

    public class GetStockByProductIdQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetStockResponse>> Handle(
            GetStockByProductIdQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var stock = await session.Query<ProductStock>()
                .FirstOrDefaultAsync(x => x.ProductId == query.ProductId, ct);

            if (stock is null)
                return FeatureObjectResultModel<GetStockResponse>.NotFound();

            return FeatureObjectResultModel<GetStockResponse>.Ok(GetStockResponse.From(stock));
        }
    }
}

[McpServerToolType]
public static class GetStockByProductIdMcpTool
{
    [McpServerTool(Name = Shared.StockTools.GetStock)]
    [Description(Shared.McpToolDescriptions.StockTools.GetStock)]
    public static Task<FeatureObjectResultModel<GetStockByProductId.GetStockResponse>> GetStockAsync(
        [Description("Stok durumu sorgulanacak urunun Id'si")] Guid productId,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<GetStockByProductId.GetStockResponse>>(
            new GetStockByProductId.GetStockByProductIdQuery(productId), ct);
}
