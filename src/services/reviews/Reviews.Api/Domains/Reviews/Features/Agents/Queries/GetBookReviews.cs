using ModelContextProtocol.Server;

namespace Reviews.Api.Domains.Reviews.Features.Agents.Queries;

// 014: anonim kitap yorumları + puan özeti. Agent query slice — MCP tool sarmalayıcısı aynı dosyada
// (conventions VSA). İnce: tool → IMessageBus → handler → IQuerySession. Yazma yok, anonim (scope yok).
// Yuzeye reviewer adı YALNIZ maskeli (R7) çıkar. Ortalama+adet ürünün TÜM görünür yorumları üzerinden
// okuma anında hesaplanır (sayfadan bağımsız).
public static class GetBookReviews
{
    public record Query(Guid ProductId, int Page, int PageSize);

    public record ReviewItem(int Rating, string? Text, DateTimeOffset CreatedAt, string Author);

    public class Response
    {
        public IReadOnlyList<ReviewItem> Items { get; set; } = [];
        public long Total { get; set; }
        public double Average { get; set; }
        public int Count { get; set; }
        public int Page { get; set; }
    }

    public class GetBookReviewsHandler(IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Query query, CancellationToken ct)
        {
            var page = query.Page < 1 ? 1 : query.Page;
            var size = query.PageSize is < 1 or > 20 ? 10 : query.PageSize;

            if (query.ProductId == Guid.Empty)
                return FeatureObjectResultModel<Response>.Ok(new Response { Page = page });

            // Özet (ortalama + adet) ürünün TÜM görünür yorumları üzerinden — sayfadan bağımsız.
            var ratings = await session.Query<Review>()
                .Where(r => r.ProductId == query.ProductId && r.Status == ReviewStatus.Visible)
                .Select(r => r.Rating)
                .ToListAsync(ct);

            var count = ratings.Count;
            var average = count == 0 ? 0d : Math.Round(ratings.Average(), 2);

            // Sayfalı yorum listesi (en yeni önce).
            var pageReviews = await session.Query<Review>()
                .Where(r => r.ProductId == query.ProductId && r.Status == ReviewStatus.Visible)
                .OrderByDescending(r => r.CreatedTime)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(ct);

            var items = pageReviews
                .Select(r => new ReviewItem(
                    r.Rating,
                    r.Text,
                    r.CreatedTime,
                    ReviewerName.Create(r.ReviewerName) is { IsSuccess: true } n ? n.Data!.Masked() : r.ReviewerName))
                .ToList();

            return FeatureObjectResultModel<Response>.Ok(new Response
            {
                Items = items,
                Total = count,
                Average = average,
                Count = count,
                Page = page,
            });
        }
    }

    [McpServerToolType]
    public static class Tool
    {
        [McpServerTool(Name = "get_book_reviews")]
        [Description("Bir kitabın yorumlarını ve puan özetini (ortalama, adet) döner. Anonim erişilebilir.")]
        public static async Task<Response> GetBookReviews(
            IMessageBus bus,
            [Description("Yorumları istenen kitabın (ürünün) kimliği")] Guid productId,
            [Description("Sayfa numarası (1'den başlar)")] int page = 1,
            [Description("Sayfa boyutu (en fazla 20)")] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<Response>>(
                new Query(productId, page, pageSize), cancellationToken);
            return result.Data ?? new Response { Page = page };
        }
    }
}
