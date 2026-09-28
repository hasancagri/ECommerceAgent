namespace Catalog.Api.Domains.Products.Features.Agents.Queries;

// 070 US1: admin tekil ürün (agent yüzeyi) — AdminGetProduct İKİZİ (bilinçli tekrar). Draft dahil;
// künye + bağlar (yazar/yayınevi/kategori ad+id) + yayın durumu + fiyat geçmişi TEK yanıtta (US1-AS2).
public static class AdminGetProduct
{
    [RequiredScope(AuthorizationScopes.AdminCatalogRead)]
    public record AdminGetProductQuery(Guid ProductId);

    public class AuthorItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    public class PriceChangeItem
    {
        public decimal? OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public DateTime ChangedAtUtc { get; set; }
    }

    public class AdminProductDetailResponse
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = default!;
        public string ShortDescription { get; set; } = default!;
        public string FullDescription { get; set; } = default!;
        public string Sku { get; set; } = default!;
        public string? Isbn { get; set; }
        public decimal Price { get; set; }
        public bool IsPublished { get; set; }
        public string? ImageUrl { get; set; }
        public List<AuthorItem> Authors { get; set; } = [];
        public Guid PublisherId { get; set; }
        public string PublisherName { get; set; } = string.Empty;
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public List<PriceChangeItem> PriceHistory { get; set; } = [];
    }

    public class AdminGetProductQueryHandler
    {
        public async Task<FeatureObjectResultModel<AdminProductDetailResponse>> Handle(
            AdminGetProductQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(query.ProductId, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<AdminProductDetailResponse>.NotFound();

            var authors = (await session.LoadManyAsync<Author>(ct, product.AuthorIds.ToArray()))
                .ToDictionary(a => a.Id, a => a.Name);
            var publisher = await session.LoadAsync<Publisher>(product.PublisherId, ct);

            // K4: dış kontrat tek kategori görür — primary = ilk atama (AdminGetProduct ile aynı sözlük).
            var primaryCategoryId = product.Categories.Select(c => c.CategoryId).FirstOrDefault();
            var category = primaryCategoryId == Guid.Empty
                ? null
                : await session.LoadAsync<Category>(primaryCategoryId, ct);

            var history = await session.Query<ProductPriceChange>()
                .Where(x => x.ProductId == product.Id)
                .OrderBy(x => x.ChangedAtUtc)
                .ToListAsync(ct);

            return FeatureObjectResultModel<AdminProductDetailResponse>.Ok(new AdminProductDetailResponse
            {
                ProductId = product.Id,
                Name = product.Name,
                ShortDescription = product.ShortDescription,
                FullDescription = product.FullDescription,
                Sku = product.Sku,
                Isbn = product.Gtin,
                Price = product.Price.Amount,
                IsPublished = product.Published,
                ImageUrl = product.ImageUrl,
                Authors = product.AuthorIds
                    .Where(authors.ContainsKey)
                    .Select(id => new AuthorItem { Id = id, Name = authors[id] })
                    .ToList(),
                PublisherId = product.PublisherId,
                PublisherName = publisher?.Name ?? string.Empty,
                CategoryId = primaryCategoryId,
                CategoryName = category?.Name ?? string.Empty,
                PriceHistory = history.Select(h => new PriceChangeItem
                {
                    OldPrice = h.OldPrice,
                    NewPrice = h.NewPrice,
                    ChangedAtUtc = h.ChangedAtUtc,
                }).ToList(),
            });
        }
    }
}

[McpServerToolType]
public static class AdminGetProductMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.GetProduct)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.GetProduct)]
    public static Task<FeatureObjectResultModel<AdminGetProduct.AdminProductDetailResponse>> AdminGetProductAsync(
        [Description("Urun kimligi (admin_list_products'tan)")] Guid productId,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminGetProduct.AdminProductDetailResponse>>(
            new AdminGetProduct.AdminGetProductQuery(productId), ct);
}
