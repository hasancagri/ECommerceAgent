namespace Catalog.Api.Domains.Products.Features.Queries;

// 018: tek kitap detayı (ISBN veya Id) — REST-only (nokta-fetch arama değil, anahtar lookup → Marten,
// Gtin unique-index). get_book MCP tool'u 018'de SÖKÜLDÜ (Catalog = kitap-only; sohbette tekil-detay
// yok, derin künye React detay sayfasından). Anonim; kişisel veri İÇERMEZ.
public static class GetBook
{
    public record Query(string? Isbn, Guid? Id);

    public class Response
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = default!;
        public string Authors { get; set; } = default!;
        public string? Publisher { get; set; }
        public string? Category { get; set; }
        public decimal? Price { get; set; }
        public bool IsSalable { get; set; }
        public int? PageCount { get; set; }
        public string? Language { get; set; }
        public string? CoverUrl { get; set; }
        public string Description { get; set; } = default!;
    }

    public class GetBookHandler(IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Query query, CancellationToken ct)
        {
            Product? product = null;
            if (query.Id is { } id && id != Guid.Empty)
                product = await session.LoadAsync<Product>(id, ct);
            else if (!string.IsNullOrWhiteSpace(query.Isbn))
            {
                var isbn = query.Isbn.Trim();
                product = await session.Query<Product>().FirstOrDefaultAsync(p => p.Gtin == isbn, ct);
            }

            if (product is null)
                return FeatureObjectResultModel<Response>.NotFound();

            var authorList = product.AuthorIds;
            var authors = authorList.Count > 0
                ? await session.Query<Author>().Where(a => authorList.Contains(a.Id)).ToListAsync(ct)
                : [];
            var publisher = product.PublisherId != Guid.Empty
                ? await session.LoadAsync<Publisher>(product.PublisherId, ct)
                : null;
            var category = product.CategoryId != Guid.Empty
                ? await session.LoadAsync<Category>(product.CategoryId, ct)
                : null;

            return FeatureObjectResultModel<Response>.Ok(new Response
            {
                Id = product.Id,
                Title = product.Name,
                Authors = string.Join(", ", authors.Select(a => a.Name)),
                Publisher = publisher?.Name,
                Category = category?.Name,
                Price = product.Price?.Amount,
                IsSalable = product.IsSalable,
                PageCount = product.PageCount,
                Language = product.Language,
                CoverUrl = product.ImageUrl,
                Description = product.Description,
            });
        }
    }
}
