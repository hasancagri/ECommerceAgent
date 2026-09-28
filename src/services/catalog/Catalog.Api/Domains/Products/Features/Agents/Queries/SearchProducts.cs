namespace Catalog.Api.Domains.Products.Features.Agents.Queries;

public static class SearchProducts
{
    // 016/052: opsiyonel kategori/yazar daraltması — ad normalize edilip Id'ye çözülür, filtre Id ile.
    public record SearchProductsQuery(string Name, string? Category = null, string? Author = null);

    // 067: DetailUrl KALDIRILDI (066 sonrası ölü route — mağaza ekransız). Kimlik + ad döner;
    // agent productId'yi get_stock/add_to_cart zincirinde kullanır.
    public class SearchProductResponse
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = null!;
    }

    public class SearchProductsQueryHandler
    {
        public async Task<FeatureObjectResultModel<SearchProductResponse>> Handle(
            SearchProductsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            // 040 FR-007: vitrin kararı Published bayrağında.
            var products = session.Query<Product>()
                .Where(x => !x.IsDeleted && x.Published &&
                            x.Name.Contains(query.Name, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                var normalized = NameNormalization.Normalize(query.Category);
                var category = await session.Query<Category>()
                    .FirstOrDefaultAsync(x => x.NormalizedName == normalized, ct);
                if (category is null)
                    return FeatureObjectResultModel<SearchProductResponse>.Ok(null); // bilinmeyen kategori → sonuç yok

                // 040 K4: kategori artık çoklu atama koleksiyonudur. LINQ Categories.Any(...) KULLANMA:
                // Newtonsoft koleksiyonu $type/$values sarmalayıcısıyla yazar, Marten'ın containment
                // çevirisi düz dizi bekler → 0 eşleşme (canlıda kanıtlandı). Ham JSONB predicate şart.
                products = products.Where(x => x.MatchesSql(
                    "d.data -> 'Categories' -> '$values' @> CAST(? as jsonb)",
                    $"[{{\"CategoryId\": \"{category.Id}\"}}]"));
            }

            if (!string.IsNullOrWhiteSpace(query.Author))
            {
                var normalized = NameNormalization.Normalize(query.Author);
                var author = await session.Query<Author>()
                    .FirstOrDefaultAsync(x => x.NormalizedName == normalized, ct);
                if (author is null)
                    return FeatureObjectResultModel<SearchProductResponse>.Ok(null); // bilinmeyen yazar → sonuç yok

                // 052: çok-yazar jsonb dizi üyeliği (List<Guid>.Contains → Marten containment).
                products = products.Where(x => x.AuthorIds.Contains(author.Id));
            }

            var row = await products
                .OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Name })
                .FirstOrDefaultAsync(ct);

            var response = row is null
                ? null
                : new SearchProductResponse { ProductId = row.Id, Name = row.Name };

            return FeatureObjectResultModel<SearchProductResponse>.Ok(response);
        }
    }
}

[McpServerToolType]
public static class GetProductByNameMcpTool
{
    [McpServerTool(Name = Shared.CatalogTools.SearchProducts)]
    [Description(Shared.McpToolDescriptions.CatalogTools.SearchProducts)]
    public static Task<FeatureObjectResultModel<SearchProducts.SearchProductResponse>> SearchProductsAsync(
        [Description("Aranacak urun adi (kismi eslesme yeterli)")] string name,
        IMessageBus bus,
        CancellationToken ct,
        [Description("Opsiyonel kategori adi (tam ad; buyuk/kucuk harf ve bosluk toleransli)")] string? category = null,
        [Description("Opsiyonel yazar adi (tam ad; buyuk/kucuk harf ve bosluk toleransli)")] string? author = null)
        => bus.InvokeAsync<FeatureObjectResultModel<SearchProducts.SearchProductResponse>>(
            new SearchProducts.SearchProductsQuery(name, category, author), ct);
}
