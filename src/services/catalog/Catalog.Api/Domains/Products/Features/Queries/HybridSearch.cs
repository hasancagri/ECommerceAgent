using Catalog.Api.Search;
using Catalog.Api.Search.Embedding;

namespace Catalog.Api.Domains.Products.Features.Queries;

// 011 US3: hibrit arama — BM25 (kelime) + kNN (anlam) iki sıralamayı RRF ile birleştirir (konum-tabanlı,
// skor normalize GEREKMEZ). Embedding yoksa BM25'e düşer (tek-ayak hâlâ çalışır). Salt-okur; host-gerekli.
public static class HybridSearch
{
    private const int CandidateDepth = 100; // her ayaktan top-N aday (RRF bunları birleştirir).

    public record HybridSearchQuery(string? Query, int? Limit, int? Offset);

    public class HybridSearchQueryHandler(ProductSearchQueries queries, QueryEmbeddingClient embedder)
    {
        public async Task<FeatureListResultModel<ProductSummary>> Handle(
            HybridSearchQuery query, CancellationToken ct)
        {
            if (SearchParameters.IsBlankQuery(query.Query))
                return FeatureListResultModel<ProductSummary>.Ok([]);

            var text = SearchParameters.NormalizeQuery(query.Query!);
            var limit = SearchParameters.NormalizeLimit(query.Limit);
            var offset = SearchParameters.NormalizeOffset(query.Offset);

            var bm25 = await queries.Bm25Async(text, CandidateDepth, 0, ct);

            var vector = await embedder.EmbedAsync(text, ct);
            IReadOnlyList<Guid> fused;
            if (vector is null)
            {
                fused = bm25; // anlam ayağı yoksa yalnız kelime (hibrit tek-ayağa düşer).
            }
            else
            {
                var semantic = await queries.SemanticAsync(vector, CandidateDepth, 0, ct);
                fused = RrfFusion.Combine(bm25, semantic);
            }

            var page = fused.Skip(offset).Take(limit).ToList();
            var products = await queries.ResolveOrderedAsync(page, ct);
            return FeatureListResultModel<ProductSummary>.Ok(products.Select(ProductSummary.From).ToList());
        }
    }
}
