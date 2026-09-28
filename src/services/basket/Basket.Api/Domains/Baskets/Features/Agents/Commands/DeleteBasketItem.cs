namespace Basket.Api.Domains.Baskets.Features.Agents.Commands;

public static class DeleteBasketItem
{
    [RequiredScope(AuthorizationScopes.BasketWrite)]
    public record DeleteBasketItemCommand(Guid UserId, Guid Id);

    public class DeleteBasketItemResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class DeleteBasketItemCommandHandler
    {
        public async Task<FeatureObjectResultModel<DeleteBasketItemResponse>> Handle(
            DeleteBasketItemCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var basket = await session.Query<Basket>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);

            if (basket is null)
                return FeatureObjectResultModel<DeleteBasketItemResponse>.NotFound();

            var result = basket.RemoveItem(cmd.Id);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<DeleteBasketItemResponse>.Error(result.Messages);

            session.Store(basket);
            return FeatureObjectResultModel<DeleteBasketItemResponse>.Ok(new DeleteBasketItemResponse { Id = basket.Id });
        }
    }
}

[McpServerToolType]
public static class DeleteBasketItemMcpTool
{
    [McpServerTool(Name = Shared.BasketTools.RemoveBasketItem)]
    [Description(Shared.McpToolDescriptions.BasketTools.RemoveBasketItem)]
    public static Task<FeatureObjectResultModel<DeleteBasketItem.DeleteBasketItemResponse>> DeleteBasketItemAsync(
        [Description("Sepetten cikarilacak urunun (sepet item) Id'si")] Guid itemId,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<DeleteBasketItem.DeleteBasketItemResponse>>(
            new DeleteBasketItem.DeleteBasketItemCommand(userId, itemId), ct);
    }
}
