namespace Catalog.Api.Domains.Products.Features.Agents.Commands;

// admin ürün ölçü ayarı (agent yüzeyi) — REST SetProductDimensions ikizi.
public static class AdminSetProductDimensions
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    public record AdminSetProductDimensionsCommand(
        Guid UserId, Guid ProductId, decimal Weight, decimal Length, decimal Width, decimal Height);

    public class AdminSetProductDimensionsResponse
    {
        public Guid ProductId { get; set; }
    }

    [Transactional]
    public class AdminSetProductDimensionsCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminSetProductDimensionsResponse>> Handle(
            AdminSetProductDimensionsCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.ProductId, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<AdminSetProductDimensionsResponse>.NotFound();

            var dimensions = ProductDimensions.Create(cmd.Weight, cmd.Length, cmd.Width, cmd.Height);
            if (dimensions is null)
                return FeatureObjectResultModel<AdminSetProductDimensionsResponse>.Error(new MessageItem
                { Property = nameof(cmd.Weight), Code = CatalogResourceConstants.PRODUCT_DIMENSIONS_INVALID });

            var set = product.SetDimensions(dimensions);
            if (!set.IsSuccess)
                return FeatureObjectResultModel<AdminSetProductDimensionsResponse>.Error(set.Messages);

            session.Store(product);
            return FeatureObjectResultModel<AdminSetProductDimensionsResponse>.Ok(
                new AdminSetProductDimensionsResponse { ProductId = product.Id });
        }
    }
}

[McpServerToolType]
public static class AdminSetProductDimensionsMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.SetProductDimensions)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.SetProductDimensions)]
    public static Task<FeatureObjectResultModel<AdminSetProductDimensions.AdminSetProductDimensionsResponse>> AdminSetProductDimensionsAsync(
        [Description("Urun kimligi")] Guid productId,
        [Description("Agirlik")] decimal weight,
        [Description("Boy (length)")] decimal length,
        [Description("En (width)")] decimal width,
        [Description("Yukseklik (height)")] decimal height,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminSetProductDimensions.AdminSetProductDimensionsResponse>>(
            new AdminSetProductDimensions.AdminSetProductDimensionsCommand(
                userId, productId, weight, length, width, height), ct);
    }
}
