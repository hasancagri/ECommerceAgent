namespace Catalog.Api.Domains.Products;

// 011: arama yüzeyi. Müşteri uçları ANONİM (keşif login istemez, FR-013) — gateway /api/catalog/**;
// admin embedding ingest X-Admin-Api-Key korumalı — gateway /api/admin/catalog/**. Arama uçları
// SONUÇ YOKSA 200 + boş liste döner (NotFound=404 keşif UX'ini bozar; boş sorgu hata değil, FR-003).
public static class ProductEndpointExtension
{

    // Query string sort → SortOption (null = alaka/popülerlik). React + MCP aynı sözlüğü kullanır.
    private static SortOption? ParseSort(string? sort) => sort?.Trim().ToLowerInvariant() switch
    {
        "price_asc" => SortOption.PriceAsc,
        "price_desc" => SortOption.PriceDesc,
        "newest" => SortOption.Newest,
        "popular" => SortOption.Popular,
        _ => null,
    };

    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var search = app.MapGroup("/api/catalog/products").WithTags("CatalogSearch");

        // GET /api/catalog/products/search — TEK birleşik yol (018; mode KALKTI). Metin + tüm filtre/
        // sort/sayfalama tek SearchRequest'e dolar → ProductSearchQueries.SearchAsync. Boş sorgu +
        // filtre-only + browse aynı uç. Anonim; sonuç yoksa 200 + boş liste.
        search.MapGet("/search", async (
            [FromQuery] string? q,
            [FromQuery] string? publisher,
            [FromQuery] string? excludePublisher,
            [FromQuery] int? binding,
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            [FromQuery] int? minPageCount,
            [FromQuery] int? maxPageCount,
            [FromQuery] string[]? genre,
            [FromQuery] string[]? mood,
            [FromQuery] string[]? audienceLevel,
            [FromQuery] string[]? theme,
            [FromQuery] bool? onlySalable,
            [FromQuery] bool? onlyDiscounted,
            [FromQuery] bool? onlyInStock,
            [FromQuery] string? sort,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            // 021: sözlük-dışı facet değeri reddedilir (FR-014) — anlaşılır 400 (filtre sessizce boş değil).
            if (TaxonomyFacetValidator.FirstInvalid(genre, mood, audienceLevel, theme) is { } invalid)
                return Results.BadRequest(new { error = "CATALOG_TAXONOMY_VALUE_INVALID", value = invalid });

            // 014 US4 (F1): UserSearched yayını burada DEĞİL → tek kaynak ScoutAgent search_books. Saf okuma.
            var request = new SearchRequest(
                Text: q,
                Publisher: publisher,
                ExcludePublisher: excludePublisher,
                PriceMin: minPrice,
                PriceMax: maxPrice,
                PageMin: minPageCount,
                PageMax: maxPageCount,
                Binding: binding is { } b ? (BindingType)b : null,
                OnlySalable: onlySalable ?? false,
                OnlyDiscounted: onlyDiscounted ?? false,
                Genres: genre,
                Moods: mood,
                AudienceLevels: audienceLevel,
                Themes: theme,
                OnlyInStock: onlyInStock ?? false,
                Sort: ParseSort(sort),
                Page: page is { } p && p > 0 ? p : 1,
                PageSize: SearchParameters.NormalizeLimit(pageSize));

            var result = await bus.InvokeAsync<FeatureObjectResultModel<SearchProducts.Response>>(
                new SearchProducts.Query(request), ct);
            return Results.Ok(result.Data ?? new SearchProducts.Response());
        });

        // 015: GET /api/catalog/products/{isbn} — tek kitap detayı (anonim; React detay sayfası çağırır).
        // URL /kitap/{slug}-{isbn}; React sondaki ISBN'i ayıklar. Lookup ISBN (Gtin unique). GetBook
        // query'si paylaşılır (MCP tool'u + bu REST aynı künye mantığını kullanır). Bulunamazsa 404.
        search.MapGet("/{isbn}", async (string isbn, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<Features.Queries.GetBook.Response>>(
                new Features.Queries.GetBook.Query(isbn, null), ct);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
        });
    }
}
