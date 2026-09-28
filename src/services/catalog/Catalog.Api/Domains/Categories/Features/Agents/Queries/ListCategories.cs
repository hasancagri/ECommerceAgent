namespace Catalog.Api.Domains.Categories.Features.Agents.Queries;

// 067 taşıma (kullanıcı kararı): envanter listeleri Catalog'da — Author/Publisher/Category otoritesi
// burası (Storefront kitap-listesi/arama yüzeyi olarak kalır). Sızıntı guard'ı: yalnız YAYINDAKİ
// (Published) en az bir üründe kullanılan kategoriler (FR-006 ruhu). Ağaç bilgisi bonus: yalnız
// Catalog bilir (ParentCategoryId) — Storefront düz ad taşıyordu.
public static class ListCategories
{
    [Cached("agent-lists", 60)]
    public record ListCategoriesQuery();

    public class CategoryItem
    {
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = null!;
        public string? ParentCategory { get; set; }
        public int ProductCount { get; set; }
    }

    // Saf çekirdek: yayındaki ürün atamalarından distinct kategori + ürün sayısı; üst kategori adı
    // ağaçtan çözülür; ada göre sıralı.
    public static List<CategoryItem> Build(
        IEnumerable<Products.Product> publishedProducts, IReadOnlyList<Category> allCategories)
    {
        var byId = allCategories.ToDictionary(c => c.Id);

        return
        [
            .. publishedProducts
                .SelectMany(p => p.Categories.Select(a => a.CategoryId).Distinct())
                .GroupBy(id => id)
                .Where(g => byId.ContainsKey(g.Key))
                .Select(g => new CategoryItem
                {
                    CategoryId = g.Key,
                    Name = byId[g.Key].Name,
                    ParentCategory = byId[g.Key].ParentCategoryId is { } pid && byId.TryGetValue(pid, out var parent)
                        ? parent.Name
                        : null,
                    ProductCount = g.Count()
                })
                .OrderBy(x => x.Name)
        ];
    }

    public class ListCategoriesQueryHandler
    {
        public async Task<FeatureListResultModel<CategoryItem>> Handle(
            ListCategoriesQuery query, IQuerySession session, CancellationToken ct)
        {
            var products = await session.Query<Products.Product>()
                .Where(x => x.Published).ToListAsync(ct);
            var categories = await session.Query<Category>().ToListAsync(ct);

            return FeatureListResultModel<CategoryItem>.Ok(Build(products, categories));
        }
    }
}

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agents slice'larini cagirir.
[McpServerToolType]
public static class ListCategoriesMcpTool
{
    [McpServerTool(Name = Shared.CatalogTools.ListCategories)]
    [Description(Shared.McpToolDescriptions.CatalogTools.ListCategories)]
    public static Task<FeatureListResultModel<ListCategories.CategoryItem>> ListCategoriesAsync(
        IMessageBus bus, CancellationToken ct)
        => bus.InvokeAsync<FeatureListResultModel<ListCategories.CategoryItem>>(
            new ListCategories.ListCategoriesQuery(), ct);
}
