namespace Catalog.Api.Domains.ProductTags.Features.Agents.Commands;

// parite: etiket ADI değiştirme (agent yüzeyi) — RenameProductTag İKİZİ (bilinçli tekrar).
// Bulunamadı → NotFound döner.
public static class AdminRenameProductTag
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    public record AdminRenameProductTagCommand(Guid UserId, Guid TagId, string Name);

    public class AdminRenameProductTagResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    [Transactional]
    public class AdminRenameProductTagCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminRenameProductTagResponse>> Handle(
            AdminRenameProductTagCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var tag = await session.LoadAsync<ProductTag>(cmd.TagId, ct);
            if (tag is null || tag.IsDeleted)
                return FeatureObjectResultModel<AdminRenameProductTagResponse>.NotFound();

            var rename = tag.Rename(cmd.Name);
            if (!rename.IsSuccess)
                return FeatureObjectResultModel<AdminRenameProductTagResponse>.Error(rename.Messages);

            session.Store(tag);

            return FeatureObjectResultModel<AdminRenameProductTagResponse>.Ok(
                new AdminRenameProductTagResponse { Id = tag.Id, Name = tag.Name });
        }
    }
}

[McpServerToolType]
public static class AdminRenameProductTagMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.RenameProductTag)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.RenameProductTag)]
    public static Task<FeatureObjectResultModel<AdminRenameProductTag.AdminRenameProductTagResponse>> AdminRenameProductTagAsync(
        [Description("Etiket kimligi (admin_list_product_tags'ten)")] Guid tagId,
        [Description("Yeni etiket adi")] string name,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminRenameProductTag.AdminRenameProductTagResponse>>(
            new AdminRenameProductTag.AdminRenameProductTagCommand(userId, tagId, name), ct);
    }
}
