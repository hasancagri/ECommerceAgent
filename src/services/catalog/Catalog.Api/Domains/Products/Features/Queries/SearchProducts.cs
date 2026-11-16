using Catalog.Api.Search;
using Catalog.Api.Search.Embedding;

namespace Catalog.Api.Domains.Products.Features.Queries;

// 011 US1+US2: kitap arama (anonim keşif). mode=keyword (BM25, embedding'siz MVP) | semantic (kNN,
// sorgu-anı embedding). Hibrit ayrı slice (HybridSearch). Boş sorgu → boş liste (hata YOK, FR-003).
// Salt-okur; aggregate mutasyonu yok. SQL host-gerekli → canlı doğrulanır (İlke VI saf-domain dışı).
public static class SearchProducts
{
    public enum SearchMode { Keyword, Semantic }

    public record SearchProductsQuery(string? Query, int? Limit, int? Offset, SearchMode Mode);

    public class SearchProductsQueryHandler(ProductSearchQueries queries, QueryEmbeddingClient embedder)
    {
        public async Task<FeatureListResultModel<ProductSummary>> Handle(
            SearchProductsQuery query, CancellationToken ct)
        {
            if (SearchParameters.IsBlankQuery(query.Query))
                return FeatureListResultModel<ProductSummary>.Ok([]); // boş sorgu = güvenli boş sonuç.

            var text = SearchParameters.NormalizeQuery(query.Query!);
            var limit = SearchParameters.NormalizeLimit(query.Limit);
            var offset = SearchParameters.NormalizeOffset(query.Offset);

            IReadOnlyList<Guid> ids;
            if (query.Mode == SearchMode.Semantic)
            {
                var vector = await embedder.EmbedAsync(text, ct);
                if (vector is null) return FeatureListResultModel<ProductSummary>.Ok([]); // embedding yok → boş.
                ids = await queries.SemanticAsync(vector, limit, offset, ct);
            }
            else
            {
                ids = await queries.Bm25Async(text, limit, offset, ct);
            }

            var products = await queries.ResolveOrderedAsync(ids, ct);
            return FeatureListResultModel<ProductSummary>.Ok(products.Select(ProductSummary.From).ToList());
        }
    }
}
