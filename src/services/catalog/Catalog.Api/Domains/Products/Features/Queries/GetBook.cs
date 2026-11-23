namespace Catalog.Api.Domains.Products.Features.Queries;

// 018: tek kitap detayı (ISBN veya Id) — REST-only (nokta-fetch arama değil, anahtar lookup → Marten,
// Gtin unique-index). get_book MCP tool'u 018'de SÖKÜLDÜ (Catalog = kitap-only; sohbette tekil-detay
// yok, derin künye React detay sayfasından). Anonim; kişisel veri İÇERMEZ. Yanıt React BookDetailDto
// sözleşmesinin TAMAMINI doldurur (stok/indirim/sayaç/cilt/zincir dahil — eksik alan "boş" render eder).
public static class GetBook
{
    public record Query(string? Isbn, Guid? Id);

    public class Response
    {
        public Guid Id { get; set; }
        public string Isbn { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string Authors { get; set; } = default!;
        public IReadOnlyList<string> Translators { get; set; } = [];
        public string? Publisher { get; set; }
        public decimal? Price { get; set; }
        public decimal? DiscountRate { get; set; } // etkin oran (0-1); liste fiyatı ezilmez (003)
        public int StockCount { get; set; }
        public bool IsSalable { get; set; }
        public int? PageCount { get; set; }
        public string? PublishedOn { get; set; } // ISO yyyy-MM-dd
        public string? Language { get; set; }
        public string? Binding { get; set; } // cilt tipi (okunur etiket); Unknown → null
        public int? Rating { get; set; } // 0-5 snapshot
        public int RatingCount { get; set; }
        public int PurchaseCount { get; set; }
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

            return FeatureObjectResultModel<Response>.Ok(new Response
            {
                Id = product.Id,
                Isbn = product.Gtin,
                Title = product.Name,
                Authors = string.Join(", ", authors.Select(a => a.Name)),
                Translators = product.Translators.ToList(),
                Publisher = publisher?.Name,
                Price = product.Price?.Amount,
                DiscountRate = product.DiscountRate,
                StockCount = product.StockCount,
                IsSalable = product.IsSalable,
                PageCount = product.PageCount,
                PublishedOn = product.PublishedOn?.ToString("yyyy-MM-dd"),
                Language = product.Language,
                Binding = BindingLabel(product.Binding),
                Rating = product.Rating,
                RatingCount = product.RatingCount,
                PurchaseCount = product.PurchaseCount,
                CoverUrl = product.ImageUrl,
                Description = product.Description,
            });
        }

        // Cilt tipi → okunur etiket (React doğrudan gösterir). Unknown → null (satır render edilmez).
        private static string? BindingLabel(BindingType binding) => binding switch
        {
            BindingType.KartonKapak => "Karton Kapak",
            BindingType.Ciltli => "Ciltli",
            BindingType.KendindenKapak => "Kendinden Kapak",
            BindingType.Plastik => "Plastik Kapak",
            _ => null,
        };
    }
}
