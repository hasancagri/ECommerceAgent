namespace Catalog.Api.Domains.ProductTags.Features.Agents.Commands;

// 070 parite: etiket OLUŞTURMA (agent yüzeyi) — CreateProductTag İKİZİ (bilinçli tekrar). Etiket dış
// kontrat taşımaz (Storefront/event yok) — yalnız Catalog içi kayıt.
public static class AdminCreateProductTag
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    public record AdminCreateProductTagCommand(Guid UserId, string Name);

    public class AdminCreateProductTagResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    [Transactional]
    public class AdminCreateProductTagCommandHandler
    {
        public Task<FeatureObjectResultModel<AdminCreateProductTagResponse>> Handle(
            AdminCreateProductTagCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Ad zorunluluğu handler'da (factory düz aggregate döner — ProductTag.Create notu).
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return Task.FromResult(FeatureObjectResultModel<AdminCreateProductTagResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Name),
                    Code = CatalogResourceConstants.TAG_NAME_REQUIRED
                }));

            var tag = ProductTag.Create(cmd.Name);
            session.Store(tag);

            return Task.FromResult(FeatureObjectResultModel<AdminCreateProductTagResponse>.Ok(
                new AdminCreateProductTagResponse { Id = tag.Id, Name = tag.Name }));
        }
    }
}

// 070/085: ADMIN tool'lari — TEK /mcp ucunda, token scope'una göre budanmış yayinlanir (Program.cs oturum filtresi);
// anonim /mcp kesif seti DEGISMEZ. Kullanici token'dan (ICurrentUser); scope katmani handler'da
// [RequiredScope(ProductTagCreate)].
// TUZAK: her opsiyonel parametrenin DEFAULT'u var (LLM parametre atlarsa ArgumentException olmasin).
[McpServerToolType]
public static class AdminCreateProductTagMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.CreateProductTag)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.CreateProductTag)]
    public static Task<FeatureObjectResultModel<AdminCreateProductTag.AdminCreateProductTagResponse>> AdminCreateProductTagAsync(
        [Description("Yeni etiket adi")] string name,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminCreateProductTag.AdminCreateProductTagResponse>>(
            new AdminCreateProductTag.AdminCreateProductTagCommand(userId, name), ct);
    }
}
