namespace Reviews.Api.Domains.Reviews.Features.Agents.Queries;

// 064: MCP okuma slice'ı — agent bir ürünün görünür yorumlarını (maskeli ad + puan + metin + tarih)
// getirir. İzole handler (konvansiyon). Hidden hariç, en yeni üstte, ilk sayfa (agent özet ister).
public static class GetProductReviews
{
    public const int PageSize = 10;

    public record GetProductReviewsQuery(Guid ProductId, int PageNumber = 1);

    public class ReviewItem
    {
        public string MaskedName { get; set; } = null!;
        public int Rating { get; set; }
        public string? Text { get; set; }
        public DateTime CreatedTime { get; set; }
    }

    public class GetProductReviewsQueryHandler
    {
        public async Task<FeatureListResultModel<ReviewItem>> Handle(
            GetProductReviewsQuery query, IQuerySession session, CancellationToken ct)
        {
            var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;

            var reviews = await session.Query<Review>()
                .Where(x => x.ProductId == query.ProductId && x.Status == ReviewStatus.Visible)
                .OrderByDescending(x => x.CreatedTime)
                .Skip((pageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync(ct);

            var items = reviews.Select(r => new ReviewItem
            {
                MaskedName = ReviewerName.Create(r.ReviewerName).Data!.Masked(),
                Rating = r.Rating,
                Text = r.Text,
                CreatedTime = r.CreatedTime,
            }).ToList();

            return FeatureListResultModel<ReviewItem>.Ok(items);
        }
    }
}

[McpServerToolType]
public static class GetProductReviewsMcpTool
{
    [McpServerTool(Name = Shared.ReviewsTools.GetReviews)]
    [Description(Shared.McpToolDescriptions.ReviewsTools.GetReviews)]
    public static Task<FeatureListResultModel<GetProductReviews.ReviewItem>> GetProductReviewsAsync(
        [Description("Urun kimligi")] Guid productId,
        IMessageBus bus,
        CancellationToken ct,
        [Description("Sayfa numarasi (varsayilan 1)")] int page = 1)
        => bus.InvokeAsync<FeatureListResultModel<GetProductReviews.ReviewItem>>(
            new GetProductReviews.GetProductReviewsQuery(productId, page), ct);
}
