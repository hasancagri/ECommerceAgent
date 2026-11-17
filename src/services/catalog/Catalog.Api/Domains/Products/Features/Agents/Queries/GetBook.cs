using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Catalog.Api.Domains.Products.Features.Agents.Queries;

// 007 US1: tek kitap detayı (ISBN veya Id). Anonim; MCP tool sarmalayıcısı aynı dosyada.
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

    [McpServerToolType]
    public static class Tool
    {
        [McpServerTool(Name = "get_book")]
        [Description("Tek bir kitabın detayını ISBN veya kitap kimliğiyle döner (ad, yazar, yayınevi, " +
                     "kategori, fiyat, sayfa, dil, kapak, açıklama). Anonim; kişisel veri İÇERMEZ.")]
        public static async Task<Response?> GetBook(
            IMessageBus bus,
            [Description("Kitabın ISBN'i (opsiyonel; id verilmezse kullanılır)")] string? isbn = null,
            [Description("Kitabın kimliği (opsiyonel GUID metni)")] string? id = null,
            CancellationToken cancellationToken = default)
        {
            Guid? parsedId = Guid.TryParse(id, out var g) ? g : null;
            var result = await bus.InvokeAsync<FeatureObjectResultModel<Response>>(
                new Query(isbn, parsedId), cancellationToken);
            return result.IsSuccess ? result.Data : null;
        }
    }
}
