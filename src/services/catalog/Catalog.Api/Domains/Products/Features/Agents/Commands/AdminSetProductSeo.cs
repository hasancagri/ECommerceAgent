namespace Catalog.Api.Domains.Products.Features.Agents.Commands;

// admin ürün SEO ayarı (agent yüzeyi) — REST SetProductSeo ikizi.
public static class AdminSetProductSeo
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    public record AdminSetProductSeoCommand(
        Guid UserId, Guid ProductId, string? MetaTitle, string? MetaKeywords, string? MetaDescription);

    public class AdminSetProductSeoResponse
    {
        public Guid ProductId { get; set; }
    }

    [Transactional]
    public class AdminSetProductSeoCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminSetProductSeoResponse>> Handle(
            AdminSetProductSeoCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.ProductId, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<AdminSetProductSeoResponse>.NotFound();

            var set = product.SetSeo(SeoMetadata.Create(cmd.MetaTitle, cmd.MetaKeywords, cmd.MetaDescription));
            if (!set.IsSuccess)
                return FeatureObjectResultModel<AdminSetProductSeoResponse>.Error(set.Messages);

            session.Store(product);
            return FeatureObjectResultModel<AdminSetProductSeoResponse>.Ok(
                new AdminSetProductSeoResponse { ProductId = product.Id });
        }
    }
}

[McpServerToolType]
public static class AdminSetProductSeoMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.SetProductSeo)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.SetProductSeo)]
    public static Task<FeatureObjectResultModel<AdminSetProductSeo.AdminSetProductSeoResponse>> AdminSetProductSeoAsync(
        [Description("Urun kimligi")] Guid productId,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        [Description("Meta baslik")] string? metaTitle = null,
        [Description("Meta anahtar kelimeler")] string? metaKeywords = null,
        [Description("Meta aciklama")] string? metaDescription = null)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminSetProductSeo.AdminSetProductSeoResponse>>(
            new AdminSetProductSeo.AdminSetProductSeoCommand(
                userId, productId, metaTitle, metaKeywords, metaDescription), ct);
    }
}