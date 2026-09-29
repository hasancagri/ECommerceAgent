namespace Catalog.Api.Domains.SpecificationAttributes.Features.Agents.Commands;

// REST admin yüzeyi söküldü — CreateSpecificationAttribute İKİZİ (bilinçli tekrar) agent yüzeyi.
// Kanonik özellik tanımı oluşturur (043); teklik NormalizedName.
public static class AdminCreateSpecificationAttribute
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    public record AdminCreateSpecificationAttributeCommand(
        Guid UserId,
        string Name,
        bool Filterable,
        int DisplayOrder);

    public class AdminCreateSpecificationAttributeResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class AdminCreateSpecificationAttributeCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminCreateSpecificationAttributeResponse>> Handle(
            AdminCreateSpecificationAttributeCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var created = SpecificationAttribute.Create(cmd.Name, cmd.Filterable, cmd.DisplayOrder);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<AdminCreateSpecificationAttributeResponse>.Error(created.Messages);

            var attribute = created.Data!;
            var exists = await session.Query<SpecificationAttribute>()
                .AnyAsync(x => x.NormalizedName == attribute.NormalizedName, ct);
            if (exists)
            {
                return FeatureObjectResultModel<AdminCreateSpecificationAttributeResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = CatalogResourceConstants.SPEC_ALREADY_EXISTS });
            }

            session.Store(attribute);

            return FeatureObjectResultModel<AdminCreateSpecificationAttributeResponse>.Ok(
                new AdminCreateSpecificationAttributeResponse { Id = attribute.Id });
        }
    }
}

// 074/085: özellik tanımı admin parite tool'ları — TEK /mcp ucunda, scope-budamalı yayınlanır
// (anonim /mcp keşif seti DEĞİŞMEZ). Yazma tool'ları kullanıcı token'dan (ICurrentUser); scope
// katmanı handler'da [RequiredScope(SpecificationAttributeCreate)].
// TUZAK: her opsiyonel parametrenin DEFAULT'u var (LLM parametre atlarsa ArgumentException olmasin).
[McpServerToolType]
public static class AdminCreateSpecificationAttributeMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.CreateSpecificationAttribute)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.CreateSpecificationAttribute)]
    public static Task<FeatureObjectResultModel<AdminCreateSpecificationAttribute.AdminCreateSpecificationAttributeResponse>> AdminCreateSpecificationAttributeAsync(
        [Description("Ozellik tanimi adi (ornek: Renk)")] string name,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        [Description("Vitrin facet filtresine girsin mi (varsayilan false)")] bool filterable = false,
        [Description("Gorunum sirasi (kucuk once; varsayilan 0)")] int displayOrder = 0)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminCreateSpecificationAttribute.AdminCreateSpecificationAttributeResponse>>(
            new AdminCreateSpecificationAttribute.AdminCreateSpecificationAttributeCommand(
                userId, name, filterable, displayOrder), ct);
    }
}
