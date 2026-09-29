namespace Catalog.Api.Domains.Products.Features.Agents.Queries;

public static class GetProduct
{
    public record GetProductQuery(string Name);

    public class GetProductResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class GetProductQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetProductResponse>> Handle(
            GetProductQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            // FR-007: vitrin kararı Published bayrağında; fiyat dışa decimal görünür (K2).
            var product = await session.Query<Product>()
                .Where(x => !x.IsDeleted && x.Published &&
                            x.Name.Contains(query.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name)
                .Select(x => new GetProductResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    Price = x.Price.Amount,
                    ImageUrl = x.ImageUrl
                })
                .FirstOrDefaultAsync(ct);

            return FeatureObjectResultModel<GetProductResponse>.Ok(product);
        }
    }
}

[McpServerToolType]
public static class GetProductMcpTool
{
    [McpServerTool(Name = Shared.CatalogTools.GetProduct)]
    [Description(Shared.McpToolDescriptions.CatalogTools.GetProduct)]
    public static Task<FeatureObjectResultModel<GetProduct.GetProductResponse>> GetProductAsync(
        [Description("Aranacak urun adi (kismi eslesme yeterli)")] string name,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<GetProduct.GetProductResponse>>(
            new GetProduct.GetProductQuery(name), ct);
}
