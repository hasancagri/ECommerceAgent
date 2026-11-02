namespace Reviews.Api.Tests;

// 044 R7: ad maskeleme goruntuleme kuralidir — ham ad saklanir, yuzeye Masked() cikar.
public class ReviewerNameTests
{
    [Fact]
    public void Create_WithValidName_ReturnsOk()
    {
        var result = ReviewerName.Create("Hasan Demiriz");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Value.ShouldBe("Hasan Demiriz");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrWhitespace_ReturnsError(string? raw)
    {
        var result = ReviewerName.Create(raw!);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == ReviewsResourceConstants.REVIEW_NAME_REQUIRED);
    }

    [Fact]
    public void Create_TrimsSurroundingWhitespace()
    {
        var result = ReviewerName.Create("  Hasan Demiriz  ");

        result.Data!.Value.ShouldBe("Hasan Demiriz");
    }

    [Fact]
    public void Masked_TwoWords_MasksEachWord()
    {
        var name = ReviewerName.Create("Hasan Demiriz").Data!;

        name.Masked().ShouldBe("H** D**");
    }

    [Fact]
    public void Masked_SingleWord_MasksSingleWord()
    {
        var name = ReviewerName.Create("Hasan").Data!;

        name.Masked().ShouldBe("H**");
    }

    [Fact]
    public void Masked_SingleLetterWord_StaysAsIs()
    {
        var name = ReviewerName.Create("Ali C").Data!;

        name.Masked().ShouldBe("A** C");
    }

    [Fact]
    public void Masked_CollapsesMultipleSpacesBetweenWords()
    {
        var name = ReviewerName.Create("Hasan  Demiriz").Data!;

        name.Masked().ShouldBe("H** D**");
    }
}