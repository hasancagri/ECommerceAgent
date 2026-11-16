using Catalog.Api.Search;

namespace Catalog.Api.Domains.Products.Features.Queries;

// 011 US2: "benzer kitaplar" — ISBN'in ürün vektörü → anlamca en yakın komşular (kendini ele).
// Vektörü olmayan ürün → boş liste (hata YOK, FR-008). Salt-okur; host-gerekli (canlı doğrulanır).
public static class SimilarProducts
{
    public record SimilarProductsQuery(string Isbn, int? Limit);

    public class SimilarProductsQueryHandler(ProductSearchQueries queries)
    {
        public async Task<FeatureListResultModel<ProductSummary>> Handle(
            SimilarProductsQuery query, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(query.Isbn))
                return FeatureListResultModel<ProductSummary>.Ok([]);

            var limit = SearchParameters.NormalizeLimit(query.Limit);
            var ids = await queries.SimilarAsync(query.Isbn.Trim(), limit, ct);
            var products = await queries.ResolveOrderedAsync(ids, ct);
            return FeatureListResultModel<ProductSummary>.Ok(products.Select(ProductSummary.From).ToList());
        }
    }
}
