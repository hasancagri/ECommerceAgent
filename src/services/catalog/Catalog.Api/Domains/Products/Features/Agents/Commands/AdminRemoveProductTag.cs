namespace Catalog.Api.Domains.Products.Features.Agents.Commands;

// 074: admin üründen etiket çıkarma (agent yüzeyi) — REST RemoveTagFromProduct ikizi.
public static class AdminRemoveProductTag
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    public record AdminRemoveProductTagCommand(Guid UserId, Guid ProductId, Guid TagId);

    public class AdminRemoveProductTagResponse
    {
        public Guid ProductId { get; set; }
        public Guid TagId { get; set; }
    }

    [Transactional]
    public class AdminRemoveProductTagCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminRemoveProductTagResponse>> Handle(
            AdminRemoveProductTagCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.ProductId, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<AdminRemoveProductTagResponse>.NotFound();

            var remove = product.RemoveTag(cmd.TagId);
            if (!remove.IsSuccess)
                return FeatureObjectResultModel<AdminRemoveProductTagResponse>.Error(remove.Messages);

            session.Store(product);
            return FeatureObjectResultModel<AdminRemoveProductTagResponse>.Ok(
                new AdminRemoveProductTagResponse { ProductId = product.Id, TagId = cmd.TagId });
        }
    }
}

[McpServerToolType]
public static class AdminRemoveProductTagMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.RemoveProductTag)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.RemoveProductTag)]
    public static Task<FeatureObjectResultModel<AdminRemoveProductTag.AdminRemoveProductTagResponse>> AdminRemoveProductTagAsync(
        [Description("Urun kimligi")] Guid productId,
        [Description("Etiket kimligi")] Guid tagId,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminRemoveProductTag.AdminRemoveProductTagResponse>>(
            new AdminRemoveProductTag.AdminRemoveProductTagCommand(userId, productId, tagId), ct);
    }
}
