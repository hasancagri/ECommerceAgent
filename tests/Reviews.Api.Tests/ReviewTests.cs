namespace Reviews.Api.Tests;

// 044 Ilke VI: Review.Create guard'lari test-first (FR-002: 1-5 tam yildiz; metin <= 2000; ad zorunlu).
public class ReviewTests
{
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);

    private static ReviewerName Name() => ReviewerName.Create("Hasan Demiriz").Data!;

    [Fact]
    public void Create_WithValidInput_ReturnsVisibleReview()
    {
        var result = Review.Create(ProductId, UserId, 4, "Gayet iyi ürün.", Name(), Now);

        result.IsSuccess.ShouldBeTrue();
        var review = result.Data!;
        review.ProductId.ShouldBe(ProductId);
        review.UserId.ShouldBe(UserId);
        review.Rating.ShouldBe(4);
        review.Text.ShouldBe("Gayet iyi ürün.");
        review.Status.ShouldBe(ReviewStatus.Visible);
    }

    [Fact]
    public void Create_WithoutText_IsAllowed()
    {
        var result = Review.Create(ProductId, UserId, 5, null, Name(), Now);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Text.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Create_WithRatingOutOfRange_ReturnsError(int rating)
    {
        var result = Review.Create(ProductId, UserId, rating, null, Name(), Now);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == ReviewsResourceConstants.REVIEW_RATING_INVALID);
    }

    [Fact]
    public void Create_WithTextOverLimit_ReturnsError()
    {
        var longText = new string('a', Review.MaxTextLength + 1);

        var result = Review.Create(ProductId, UserId, 3, longText, Name(), Now);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == ReviewsResourceConstants.REVIEW_TEXT_TOO_LONG);
    }

    [Fact]
    public void Create_WithTextAtLimit_IsAllowed()
    {
        var text = new string('a', Review.MaxTextLength);

        var result = Review.Create(ProductId, UserId, 3, text, Name(), Now);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithEmptyProductOrUser_ReturnsError()
    {
        Review.Create(Guid.Empty, UserId, 3, null, Name(), Now).IsSuccess.ShouldBeFalse();
        Review.Create(ProductId, Guid.Empty, 3, null, Name(), Now).IsSuccess.ShouldBeFalse();
    }
}