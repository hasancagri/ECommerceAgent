using Catalog.Api.Import;
using Catalog.Api.Domains.Authors;
using Catalog.Api.Domains.Publishers;
using Catalog.Api.Domains.Categories;

namespace Catalog.Api.Domains.Products.Features.Agents.Commands;

// US3/FR-009: import-kökenli, fiyat>0 taslakları TOPLU yayınlar. Köken izi = ImportRow.ProductId
// (Processed satırlar) — yalnız bu ürünlere dokunur; elle oluşturulmuş (import-dışı) taslaklar etkilenmez.
// Kapı: !Published && Price>0 (IsPublishable). Yayınlanan her ürün için ProductChangedEvent (Storefront'a
// düşer). AdminRepublishProducts bulk-fat-event deseninin ikizi (bilinçli tekrar).
public static class PublishImported
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    public record PublishImportedCommand(Guid UserId);

    public class PublishImportedResponse
    {
        public int PublishedCount { get; set; }
        public int SkippedNoPriceCount { get; set; }
    }

    // Yayın kapısı (saf, test-first): import taslağı yalnız yayınlanmamış + fiyatlıysa yayınlanır.
    public static bool IsPublishable(Product product) => !product.Published && product.Price.Amount > 0;

    [Transactional]
    public class PublishImportedCommandHandler(IDocumentSession session, IMessageBus bus)
    {
        public async Task<FeatureObjectResultModel<PublishImportedResponse>> Handle(
            PublishImportedCommand cmd, CancellationToken ct)
        {
            // Köken = import: yalnız Processed ImportRow'ların ProductId'leri (Gtin sorgusu YOK — ImportRow izi).
            var productIds = (await session.Query<ImportRow>()
                    .Where(r => r.Status == ImportRowStatus.Processed && r.ProductId != null)
                    .Select(r => r.ProductId)
                    .ToListAsync(ct))
                .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();

            var products = await session.LoadManyAsync<Product>(ct, productIds);

            // Yayınlanacakların künye adlarını toplu yükle (N+1 önle).
            var toPublish = products.Where(IsPublishable).ToList();
            var authors = (await session.LoadManyAsync<Author>(ct,
                    toPublish.SelectMany(p => p.AuthorIds).Distinct().ToArray()))
                .ToDictionary(a => a.Id);
            var publishers = (await session.LoadManyAsync<Publisher>(ct,
                    toPublish.Select(p => p.PublisherId).Distinct().ToArray()))
                .ToDictionary(p => p.Id);
            var categoryIds = toPublish
                .Select(p => p.Categories.Select(c => c.CategoryId).FirstOrDefault())
                .Where(id => id != Guid.Empty).Distinct().ToArray();
            var categories = (await session.LoadManyAsync<Category>(ct, categoryIds))
                .ToDictionary(c => c.Id);

            var published = 0;
            var skippedNoPrice = 0;
            foreach (var p in products)
            {
                if (p.Published)
                    continue; // zaten canlı (idempotent — sayıma girmez)
                if (p.Price.Amount <= 0)
                {
                    skippedNoPrice++; // fiyatsız taslak — satılamaz, dokunma
                    continue;
                }

                var result = p.Publish();
                if (!result.IsSuccess)
                {
                    skippedNoPrice++;
                    continue;
                }
                session.Store(p);

                var categoryId = p.Categories.Select(c => c.CategoryId).FirstOrDefault();
                await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                    p.Id, p.Name, p.FullDescription, p.Price.Amount,
                    p.AuthorIds.Where(authors.ContainsKey)
                        .Select(id => new IntegrationEvents.AuthorRef(id, authors[id].Name)).ToList(),
                    p.PublisherId, publishers.TryGetValue(p.PublisherId, out var pub) ? pub.Name : string.Empty,
                    categoryId, categories.TryGetValue(categoryId, out var cat) ? cat.Name : string.Empty,
                    p.ImageUrl, IsDeleted: false, OldPrice: null));
                published++;
            }

            return FeatureObjectResultModel<PublishImportedResponse>.Ok(new PublishImportedResponse
            {
                PublishedCount = published,
                SkippedNoPriceCount = skippedNoPrice
            });
        }
    }
}

[McpServerToolType]
public static class PublishImportedMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.PublishImported)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.PublishImported)]
    public static Task<FeatureObjectResultModel<PublishImported.PublishImportedResponse>> PublishImportedAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<PublishImported.PublishImportedResponse>>(
            new PublishImported.PublishImportedCommand(userId), ct);
    }
}
