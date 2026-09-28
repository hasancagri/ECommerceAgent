namespace Catalog.Api.Domains.ProductTags.Features.Agents.Queries;

// 070 parite: etiket LİSTELEME (agent yüzeyi) — GetProductTags İKİZİ (bilinçli tekrar). Salt-okur,
// audit YOK. Opsiyonel q ile ada göre (case-insensitive alt-dizge) daraltılır; ada göre sıralı.
public static class AdminListProductTags
{
    [RequiredScope(AuthorizationScopes.AdminCatalogRead)]
    public record ListProductTagsQuery(string? Search = null);

    public class ProductTagItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    public class ListProductTagsQueryHandler
    {
        public async Task<FeatureListResultModel<ProductTagItem>> Handle(
            ListProductTagsQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var tags = await session.Query<ProductTag>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .ToListAsync(ct);

            var items = tags.Select(x => new ProductTagItem { Id = x.Id, Name = x.Name });

            if (!string.IsNullOrWhiteSpace(query.Search))
                items = items.Where(t =>
                    t.Name.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase));

            return FeatureListResultModel<ProductTagItem>.Ok(items.ToList());
        }
    }
}

[McpServerToolType]
public static class AdminListProductTagsMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.ListProductTags)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.ListProductTags)]
    public static Task<FeatureListResultModel<AdminListProductTags.ProductTagItem>> AdminListProductTagsAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Etiket adinda gecen metin (bos = tumu)")] string? search = null)
        => bus.InvokeAsync<FeatureListResultModel<AdminListProductTags.ProductTagItem>>(
            new AdminListProductTags.ListProductTagsQuery(search), ct);
}
