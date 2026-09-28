namespace Catalog.Api.Domains.Products.Features.Agents.Queries;

// 070 US1: fiyat geçmişi (admin agent yüzeyi) — GetProductPriceHistory/AdminGetProduct geçmiş
// bloğunun İKİZİ (bilinçli tekrar). Draft dahil; kronolojik TAM liste. Okuma — iz yazılmaz.
public static class AdminGetPriceHistory
{
    [RequiredScope(AuthorizationScopes.AdminCatalogRead)]
    public record AdminGetPriceHistoryQuery(Guid ProductId);

    public class PriceChangeItem
    {
        public decimal? OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public DateTime ChangedAtUtc { get; set; }
    }

    public class AdminGetPriceHistoryQueryHandler
    {
        public async Task<FeatureListResultModel<PriceChangeItem>> Handle(
            AdminGetPriceHistoryQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(query.ProductId, ct);
            if (product is null || product.IsDeleted)
                return FeatureListResultModel<PriceChangeItem>.NotFound();

            var history = await session.Query<ProductPriceChange>()
                .Where(x => x.ProductId == query.ProductId)
                .OrderBy(x => x.ChangedAtUtc)
                .ToListAsync(ct);

            return FeatureListResultModel<PriceChangeItem>.Ok(history.Select(h => new PriceChangeItem
            {
                OldPrice = h.OldPrice,
                NewPrice = h.NewPrice,
                ChangedAtUtc = h.ChangedAtUtc,
            }).ToList());
        }
    }
}

[McpServerToolType]
public static class AdminGetPriceHistoryMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.GetPriceHistory)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.GetPriceHistory)]
    public static Task<FeatureListResultModel<AdminGetPriceHistory.PriceChangeItem>> AdminGetPriceHistoryAsync(
        [Description("Urun kimligi")] Guid productId,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureListResultModel<AdminGetPriceHistory.PriceChangeItem>>(
            new AdminGetPriceHistory.AdminGetPriceHistoryQuery(productId), ct);
}
