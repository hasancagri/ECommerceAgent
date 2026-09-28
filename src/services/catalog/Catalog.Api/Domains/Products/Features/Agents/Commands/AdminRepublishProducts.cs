namespace Catalog.Api.Domains.Products.Features.Agents.Commands;

// 079 backfill: tüm YAYINDAKİ ürünler için `ProductChangedEvent`'i yeniden yayınlar. Yeni bir downstream BC
// (ör. Discount `ProductCatalogRef`) canlıya alındığında ya da bir read-model sıfırlandığında, event-besleme
// geçmişi taşımadığından boş kalır — bu tool geçmişi replay eder. TÜM tüketiciler için idempotent
// (Storefront upsert + embedding "Keep"; Library OldPrice=null → alarm yok). AdminUpdateProduct fat-event
// mapping'inin toplu hâli (bilinçli tekrar). Fiyat değişimi DEĞİL → OldPrice hep null.
public static class AdminRepublishProducts
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    public record AdminRepublishProductsCommand(Guid UserId);

    public class AdminRepublishProductsResponse
    {
        public int Republished { get; set; }
        public string Message { get; set; } = default!;
    }

    public class AdminRepublishProductsCommandHandler(IDocumentSession session, IMessageBus bus)
    {
        public async Task<FeatureObjectResultModel<AdminRepublishProductsResponse>> Handle(
            AdminRepublishProductsCommand cmd, CancellationToken ct)
        {
            var products = await session.Query<Product>().Where(p => p.Published).ToListAsync(ct);

            // Künye adlarını toplu yükle (N+1 önle).
            var authors = (await session.LoadManyAsync<Author>(ct,
                    products.SelectMany(p => p.AuthorIds).Distinct().ToArray()))
                .ToDictionary(a => a.Id);
            var publishers = (await session.LoadManyAsync<Publisher>(ct,
                    products.Select(p => p.PublisherId).Distinct().ToArray()))
                .ToDictionary(p => p.Id);
            var categoryIds = products
                .Select(p => p.Categories.Select(c => c.CategoryId).FirstOrDefault())
                .Where(id => id != Guid.Empty).Distinct().ToArray();
            var categories = (await session.LoadManyAsync<Category>(ct, categoryIds))
                .ToDictionary(c => c.Id);

            var count = 0;
            foreach (var p in products)
            {
                var categoryId = p.Categories.Select(c => c.CategoryId).FirstOrDefault();
                await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                    p.Id, p.Name, p.FullDescription, p.Price.Amount,
                    p.AuthorIds.Where(authors.ContainsKey)
                        .Select(id => new IntegrationEvents.AuthorRef(id, authors[id].Name)).ToList(),
                    p.PublisherId, publishers.TryGetValue(p.PublisherId, out var pub) ? pub.Name : string.Empty,
                    categoryId, categories.TryGetValue(categoryId, out var cat) ? cat.Name : string.Empty,
                    p.ImageUrl, IsDeleted: false, OldPrice: null));
                count++;
            }

            return FeatureObjectResultModel<AdminRepublishProductsResponse>.Ok(new AdminRepublishProductsResponse
            {
                Republished = count,
                Message = $"{count} yayındaki ürün için ProductChangedEvent yeniden yayınlandı."
            });
        }
    }
}

[McpServerToolType]
public static class AdminRepublishProductsMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.RepublishProducts)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.RepublishProducts)]
    public static Task<FeatureObjectResultModel<AdminRepublishProducts.AdminRepublishProductsResponse>> RepublishProductsAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminRepublishProducts.AdminRepublishProductsResponse>>(
            new AdminRepublishProducts.AdminRepublishProductsCommand(userId), ct);
    }
}
