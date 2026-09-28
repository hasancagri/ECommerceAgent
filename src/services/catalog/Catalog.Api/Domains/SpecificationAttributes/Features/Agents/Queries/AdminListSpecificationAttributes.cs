namespace Catalog.Api.Domains.SpecificationAttributes.Features.Agents.Queries;

// 074: REST admin yüzeyi söküldü — GetSpecificationAttributes İKİZİ (bilinçli tekrar) agent yüzeyi.
// Okuma slice: audit YOK. Tüm özellik tanımları + kapalı-liste seçenekleri (043), DisplayOrder sıralı.
public static class AdminListSpecificationAttributes
{
    [RequiredScope(AuthorizationScopes.AdminCatalogRead)]
    public record ListSpecificationAttributesQuery;

    public class OptionItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public int DisplayOrder { get; set; }
    }

    public class SpecificationAttributeItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public bool Filterable { get; set; }
        public int DisplayOrder { get; set; }
        public List<OptionItem> Options { get; set; } = [];
    }

    public class ListSpecificationAttributesQueryHandler
    {
        public async Task<FeatureListResultModel<SpecificationAttributeItem>> Handle(
            ListSpecificationAttributesQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var attributes = await session.Query<SpecificationAttribute>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.DisplayOrder)
                .ToListAsync(ct);

            return FeatureListResultModel<SpecificationAttributeItem>.Ok(
                attributes.Select(a => new SpecificationAttributeItem
                {
                    Id = a.Id,
                    Name = a.Name,
                    Filterable = a.Filterable,
                    DisplayOrder = a.DisplayOrder,
                    Options = a.Options.OrderBy(o => o.DisplayOrder)
                        .Select(o => new OptionItem { Id = o.Id, Name = o.Name, DisplayOrder = o.DisplayOrder })
                        .ToList(),
                }).ToList());
        }
    }
}

[McpServerToolType]
public static class AdminListSpecificationAttributesMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.ListSpecificationAttributes)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.ListSpecificationAttributes)]
    public static Task<FeatureListResultModel<AdminListSpecificationAttributes.SpecificationAttributeItem>> AdminListSpecificationAttributesAsync(
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureListResultModel<AdminListSpecificationAttributes.SpecificationAttributeItem>>(
            new AdminListSpecificationAttributes.ListSpecificationAttributesQuery(), ct);
}
