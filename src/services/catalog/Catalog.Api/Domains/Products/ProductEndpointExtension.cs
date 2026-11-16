using Catalog.Api.Filters;
using Catalog.Api.Search;
using Catalog.Api.Domains.Products.Features.Commands;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Domains.Products;

// 011: arama yüzeyi. Müşteri uçları ANONİM (keşif login istemez, FR-013) — gateway /api/catalog/**;
// admin embedding ingest X-Admin-Api-Key korumalı — gateway /api/admin/catalog/**. Arama uçları
// SONUÇ YOKSA 200 + boş liste döner (NotFound=404 keşif UX'ini bozar; boş sorgu hata değil, FR-003).
public static class ProductEndpointExtension
{

    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var search = app.MapGroup("/api/catalog/products").WithTags("CatalogSearch");

        // GET /api/catalog/products/search?q=&mode=keyword|semantic|hybrid&limit=&offset=
        search.MapGet("/search", async (
            [FromQuery] string? q,
            [FromQuery] string? mode,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = mode?.ToLowerInvariant() switch
            {
                "semantic" => await bus.InvokeAsync<FeatureListResultModel<ProductSummary>>(
                    new SearchProducts.SearchProductsQuery(q, limit, offset, SearchProducts.SearchMode.Semantic), ct),
                "hybrid" => await bus.InvokeAsync<FeatureListResultModel<ProductSummary>>(
                    new HybridSearch.HybridSearchQuery(q, limit, offset), ct),
                _ => await bus.InvokeAsync<FeatureListResultModel<ProductSummary>>(
                    new SearchProducts.SearchProductsQuery(q, limit, offset, SearchProducts.SearchMode.Keyword), ct),
            };
            return Results.Ok(result.Data ?? new List<ProductSummary>());
        });

        // GET /api/catalog/products/{isbn}/similar?limit=
        search.MapGet("/{isbn}/similar", async (
            string isbn, [FromQuery] int? limit, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<FeatureListResultModel<ProductSummary>>(
                new SimilarProducts.SimilarProductsQuery(isbn, limit), ct);
            return Results.Ok(result.Data ?? new List<ProductSummary>());
        });

        // POST /api/admin/catalog/embeddings — offline araç çıktısı yükleme (admin-anahtar).
        var admin = app.MapGroup("/api/admin/catalog/embeddings")
            .WithTags("CatalogSearchAdmin")
            .AddEndpointFilter<AdminApiKeyEndpointFilter>();

        admin.MapPost("/", async (
            IngestEmbeddings.IngestEmbeddingsCommand command, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<IngestEmbeddings.IngestEmbeddingsResponse>>(
                command, ct);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
        });
    }
}
