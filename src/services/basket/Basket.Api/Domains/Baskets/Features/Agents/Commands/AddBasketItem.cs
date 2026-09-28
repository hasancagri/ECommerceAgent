namespace Basket.Api.Domains.Baskets.Features.Agents.Commands;

public static class AddBasketItem
{
    [RequiredScope(AuthorizationScopes.BasketWrite)]
    public record AddBasketItemCommand(
        Guid UserId,
        Guid ProductId,
        string ProductName,
        decimal Price,
        string? ImageUrl);

    public class AddBasketItemResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class AddBasketItemCommandHandler
    {
        public async Task<FeatureObjectResultModel<AddBasketItemResponse>> Handle(
            AddBasketItemCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var basket = await session.Query<Basket>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);

            var newItem = new BasketItem(cmd.ProductId, cmd.ProductName, cmd.ImageUrl, cmd.Price);

            basket ??= Basket.Create(cmd.UserId);
            var add = basket.AddItem(newItem);
            if (!add.IsSuccess)
                return FeatureObjectResultModel<AddBasketItemResponse>.Error(add.Messages);

            session.Store(basket);
            return FeatureObjectResultModel<AddBasketItemResponse>.Ok(new AddBasketItemResponse { Id = basket.Id });
        }
    }
}

[McpServerToolType]
public static class AddBasketItemMcpTool
{
    [McpServerTool(Name = Shared.BasketTools.AddToCart)]
    [Description(Shared.McpToolDescriptions.BasketTools.AddToCart)]
    public static Task<FeatureObjectResultModel<AddBasketItem.AddBasketItemResponse>> AddBasketItemAsync(
        [Description("Sepete eklenecek urunun Id'si")] Guid productId,
        [Description("Urun adi")] string productName,
        [Description("Urun fiyati (ondalikli, orn. 199.90)")] decimal price,
        [Description("Urun gorsel URL'si (opsiyonel)")] string? imageUrl,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AddBasketItem.AddBasketItemResponse>>(
            new AddBasketItem.AddBasketItemCommand(userId, productId, productName, price, imageUrl), ct);
    }
}
