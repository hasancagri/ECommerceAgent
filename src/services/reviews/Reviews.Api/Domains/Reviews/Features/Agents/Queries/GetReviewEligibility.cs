namespace Reviews.Api.Domains.Reviews.Features.Agents.Queries;

// MCP okuma slice'ı — agent kullanıcının bu ürüne yorum yapıp yapamayacağını (satın-alma
// kanıtı + tek-yorum) sorar. Nihai karar SubmitReview'da (yarışta 400). reviews.write scope.
public static class GetReviewEligibility
{
    [RequiredScope(AuthorizationScopes.ReviewsWrite)]
    public record GetReviewEligibilityQuery(Guid UserId, Guid ProductId);

    public class EligibilityResponse
    {
        public bool CanReview { get; set; }
        public string? ReasonCode { get; set; }
    }

    public class GetReviewEligibilityQueryHandler
    {
        public async Task<FeatureObjectResultModel<EligibilityResponse>> Handle(
            GetReviewEligibilityQuery query, IQuerySession session, CancellationToken ct)
        {
            var exists = await session.Query<Review>()
                .Where(x => x.UserId == query.UserId && x.ProductId == query.ProductId)
                .AnyAsync(ct);
            if (exists)
                return Result(false, ReviewsResourceConstants.REVIEW_ALREADY_EXISTS);

            var purchased = await session.LoadAsync<PurchasedProduct>(
                PurchasedProduct.KeyFor(query.UserId, query.ProductId), ct);
            if (purchased is null)
                return Result(false, ReviewsResourceConstants.REVIEW_PURCHASE_REQUIRED);

            return Result(true, null);
        }

        private static FeatureObjectResultModel<EligibilityResponse> Result(bool canReview, string? reasonCode) =>
            FeatureObjectResultModel<EligibilityResponse>.Ok(new EligibilityResponse
            {
                CanReview = canReview,
                ReasonCode = reasonCode,
            });
    }
}

[McpServerToolType]
public static class GetReviewEligibilityMcpTool
{
    [McpServerTool(Name = Shared.ReviewsTools.CheckReviewEligibility)]
    [Description(Shared.McpToolDescriptions.ReviewsTools.CheckReviewEligibility)]
    public static Task<FeatureObjectResultModel<GetReviewEligibility.EligibilityResponse>> GetReviewEligibilityAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid productId,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<GetReviewEligibility.EligibilityResponse>>(
            new GetReviewEligibility.GetReviewEligibilityQuery(userId, productId), ct);
    }
}
