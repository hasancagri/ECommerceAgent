namespace Reviews.Api.Domains.Reviews.Features.Agents.Commands;

// MCP yazma slice'ı — agent chat'ten yorum gönderir. İzole handler (SubmitReview'ın bilinçli
// tekrarı; konvansiyon: Commands'i IMessageBus ile reuse etmez). ReviewerNameRaw token claim'inden
// (MCP tool doldurur), istek gövdesinden ASLA. Satın-alma kanıtı + tek-yorum kontrolü.
public static class SubmitReview
{
    [RequiredScope(AuthorizationScopes.ReviewsWrite)]
    public record SubmitReviewCommand(
        Guid UserId, Guid ProductId, int Rating, string? Text, string ReviewerNameRaw);

    public class SubmitReviewResponse
    {
        public Guid ReviewId { get; set; }
        public string MaskedName { get; set; } = null!;
        public int Rating { get; set; }
        public string Message { get; set; } = default!;
    }

    [Transactional]
    public class SubmitReviewCommandHandler
    {
        public async Task<FeatureObjectResultModel<SubmitReviewResponse>> Handle(
            SubmitReviewCommand cmd, IDocumentSession session, IMessageBus bus, CancellationToken ct)
        {
            var name = ReviewerName.Create(cmd.ReviewerNameRaw);
            if (!name.IsSuccess)
                return FeatureObjectResultModel<SubmitReviewResponse>.Error(name.Messages);

            // FR-001: satın-alma kanıtı lokal read-model'den (OrderCompleted event-fed). Yoksa RED.
            var purchased = await session.LoadAsync<PurchasedProduct>(
                PurchasedProduct.KeyFor(cmd.UserId, cmd.ProductId), ct);
            if (purchased is null)
                return FeatureObjectResultModel<SubmitReviewResponse>.Error(
                    new MessageItem { Code = ReviewsResourceConstants.REVIEW_PURCHASE_REQUIRED });

            // Tek-yorum (uygulama kontrolü; unique index son söz).
            var exists = await session.Query<Review>()
                .Where(x => x.UserId == cmd.UserId && x.ProductId == cmd.ProductId)
                .AnyAsync(ct);
            if (exists)
                return FeatureObjectResultModel<SubmitReviewResponse>.Error(
                    new MessageItem { Code = ReviewsResourceConstants.REVIEW_ALREADY_EXISTS });

            var created = Review.Create(
                cmd.ProductId, cmd.UserId, cmd.Rating, cmd.Text, name.Data!, DateTimeOffset.UtcNow);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<SubmitReviewResponse>.Error(created.Messages);

            var review = created.Data!;
            session.Store(review);

            // Özet Reviews'ta hesaplanır; yeni yorum Visible doğar.
            var visibleCount = await session.Query<Review>()
                .Where(x => x.ProductId == cmd.ProductId && x.Status == ReviewStatus.Visible)
                .CountAsync(ct);
            var visibleSum = visibleCount == 0
                ? 0
                : await session.Query<Review>()
                    .Where(x => x.ProductId == cmd.ProductId && x.Status == ReviewStatus.Visible)
                    .SumAsync(x => x.Rating, ct);

            var count = visibleCount + 1;
            var average = Math.Round((visibleSum + review.Rating) / (decimal)count, 2);
            await bus.PublishAsync(new IntegrationEvents.ReviewSummaryChanged(cmd.ProductId, average, count));

            // Unique index son söz — yarış kaybedeni nazik hataya çevrilir.
            try
            {
                await session.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (
                ex is Npgsql.PostgresException { SqlState: "23505" }
                || ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                return FeatureObjectResultModel<SubmitReviewResponse>.Error(
                    new MessageItem { Code = ReviewsResourceConstants.REVIEW_ALREADY_EXISTS });
            }

            return FeatureObjectResultModel<SubmitReviewResponse>.Ok(new SubmitReviewResponse
            {
                ReviewId = review.Id,
                MaskedName = ReviewerName.Create(review.ReviewerName).Data!.Masked(),
                Rating = review.Rating,
                Message = "Yorumun gönderildi.",
            });
        }
    }
}

[McpServerToolType]
public static class SubmitReviewMcpTool
{
    [McpServerTool(Name = Shared.ReviewsTools.SubmitReview)]
    [Description(Shared.McpToolDescriptions.ReviewsTools.SubmitReview)]
    public static Task<FeatureObjectResultModel<SubmitReview.SubmitReviewResponse>> SubmitReviewAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid productId,
        int rating,
        CancellationToken ct,
        [Description("Opsiyonel yorum metni")] string? text = null)
    {
        var user = currentUser.Load(http.HttpContext!.User);
        // Görünen ad "name" claim'inden; boşsa CurrentUser.Name yedek (SubmitReview endpoint paritesi).
        var displayName = http.HttpContext!.User.FindFirst("name")?.Value ?? user.Name ?? string.Empty;
        return bus.InvokeAsync<FeatureObjectResultModel<SubmitReview.SubmitReviewResponse>>(
            new SubmitReview.SubmitReviewCommand(user.Id, productId, rating, text, displayName), ct);
    }
}
