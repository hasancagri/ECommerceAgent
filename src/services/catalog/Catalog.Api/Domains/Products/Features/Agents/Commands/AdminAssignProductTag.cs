namespace Catalog.Api.Domains.Products.Features.Agents.Commands;

// admin ürüne etiket atama (agent yüzeyi) — REST AssignTagToProduct ikizi.
public static class AdminAssignProductTag
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    public record AdminAssignProductTagCommand(Guid UserId, Guid ProductId, Guid TagId);

    public class AdminAssignProductTagResponse
    {
        public Guid ProductId { get; set; }
        public Guid TagId { get; set; }
    }

    [Transactional]
    public class AdminAssignProductTagCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminAssignProductTagResponse>> Handle(
            AdminAssignProductTagCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.ProductId, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<AdminAssignProductTagResponse>.NotFound();

            var tag = await session.LoadAsync<ProductTags.ProductTag>(cmd.TagId, ct);
            if (tag is null || tag.IsDeleted)
            {
                return FeatureObjectResultModel<AdminAssignProductTagResponse>.Error(new MessageItem
                { Property = nameof(cmd.TagId), Code = CatalogResourceConstants.RECORD_NOT_FOUND });
            }

            var add = product.AddTag(cmd.TagId);
            if (!add.IsSuccess)
                return FeatureObjectResultModel<AdminAssignProductTagResponse>.Error(add.Messages);

            session.Store(product);
            return FeatureObjectResultModel<AdminAssignProductTagResponse>.Ok(
                new AdminAssignProductTagResponse { ProductId = product.Id, TagId = cmd.TagId });
        }
    }
}

[McpServerToolType]
public static class AdminAssignProductTagMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.AssignProductTag)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.AssignProductTag)]
    public static Task<FeatureObjectResultModel<AdminAssignProductTag.AdminAssignProductTagResponse>> AdminAssignProductTagAsync(
        [Description("Urun kimligi")] Guid productId,
        [Description("Etiket kimligi (admin_list_product_tags'ten)")] Guid tagId,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminAssignProductTag.AdminAssignProductTagResponse>>(
            new AdminAssignProductTag.AdminAssignProductTagCommand(userId, productId, tagId), ct);
    }
}