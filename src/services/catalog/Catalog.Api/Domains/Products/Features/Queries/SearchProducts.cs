using Catalog.Api.Search;

namespace Catalog.Api.Domains.Products.Features.Queries;

// 018 US1: kitap arama (anonim keşif) — TEK birleşik yol. Metin/filtre/sort/browse hepsi SearchRequest'e
// dolar, ProductSearchQueries.SearchAsync tek ParadeDB sorgusuyla çözer (aday yükleme YOK). Boş sorgu +
// filtre-only + browse aynı yol. Salt-okur; SQL host-gerekli → canlı doğrulanır (İlke VI saf-domain dışı).
public static class SearchProducts
{
    public record Query(SearchRequest Request);

    public class Response
    {
        public IReadOnlyList<ProductSummary> Items { get; set; } = [];
        public long Total { get; set; }
        public int Page { get; set; }
    }

    public class SearchProductsQueryHandler(ProductSearchQueries queries)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Query query, CancellationToken ct)
        {
            var page = await queries.SearchAsync(query.Request, ct);
            return FeatureObjectResultModel<Response>.Ok(new Response
            {
                Items = page.Items.Select(ProductSummary.From).ToList(),
                Total = page.Total,
                Page = Math.Max(query.Request.Page, 1),
            });
        }
    }
}
