namespace Catalog.Api.Domains.SpecificationAttributes.Features.Agents.Commands;

// 074: REST admin yüzeyi söküldü — AddSpecificationAttributeOption İKİZİ (bilinçli tekrar) agent yüzeyi.
// Kapalı listeye yeni değer ekler (043); üretilen OptionId döner. Bulunamadı → NotFound.
public static class AdminAddSpecificationAttributeOption
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    public record AdminAddSpecificationAttributeOptionCommand(
        Guid UserId,
        Guid AttributeId,
        string Name,
        int DisplayOrder);

    public class AdminAddSpecificationAttributeOptionResponse
    {
        public Guid OptionId { get; set; }
    }

    [Transactional]
    public class AdminAddSpecificationAttributeOptionCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminAddSpecificationAttributeOptionResponse>> Handle(
            AdminAddSpecificationAttributeOptionCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var attribute = await session.LoadAsync<SpecificationAttribute>(cmd.AttributeId, ct);
            if (attribute is null || attribute.IsDeleted)
                return FeatureObjectResultModel<AdminAddSpecificationAttributeOptionResponse>.NotFound();

            var added = attribute.AddOption(cmd.Name, cmd.DisplayOrder);
            if (!added.IsSuccess)
                return FeatureObjectResultModel<AdminAddSpecificationAttributeOptionResponse>.Error(added.Messages);

            session.Store(attribute);

            return FeatureObjectResultModel<AdminAddSpecificationAttributeOptionResponse>.Ok(
                new AdminAddSpecificationAttributeOptionResponse { OptionId = added.Data });
        }
    }
}

[McpServerToolType]
public static class AdminAddSpecificationAttributeOptionMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.AddSpecificationAttributeOption)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.AddSpecificationAttributeOption)]
    public static Task<FeatureObjectResultModel<AdminAddSpecificationAttributeOption.AdminAddSpecificationAttributeOptionResponse>> AdminAddSpecificationAttributeOptionAsync(
        [Description("Ozellik tanimi kimligi (admin_list_specification_attributes'tan)")] Guid attributeId,
        [Description("Yeni secenek adi (ornek: Siyah)")] string name,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        [Description("Gorunum sirasi (kucuk once; varsayilan 0)")] int displayOrder = 0)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminAddSpecificationAttributeOption.AdminAddSpecificationAttributeOptionResponse>>(
            new AdminAddSpecificationAttributeOption.AdminAddSpecificationAttributeOptionCommand(
                userId, attributeId, name, displayOrder), ct);
    }
}
