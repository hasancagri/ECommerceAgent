namespace Personalization.Api.Tests;

// 012 T005 (test-first, İlke VI): UserSearchRecord.Create — login arama ham kaydı. Guard'lar:
// userId boş / query boş / resultCount<0 → Error; geçerli → Ok. Saf (host'suz).
public class UserSearchRecordTests
{
    private static readonly DateTimeOffset When = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Create_EmptyUserId_ReturnsError()
    {
        var result = UserSearchRecord.Create(Guid.Empty, "orwell", "keyword", 5, When);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == PersonalizationResourceConstants.SEARCH_USER_REQUIRED);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankQuery_ReturnsError(string query)
    {
        var result = UserSearchRecord.Create(Guid.NewGuid(), query, "keyword", 5, When);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == PersonalizationResourceConstants.SEARCH_QUERY_EMPTY);
    }

    [Fact]
    public void Create_NegativeResultCount_ReturnsError()
    {
        var result = UserSearchRecord.Create(Guid.NewGuid(), "orwell", "keyword", -1, When);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == PersonalizationResourceConstants.SEARCH_RESULT_COUNT_INVALID);
    }

    [Fact]
    public void Create_Valid_ReturnsOk()
    {
        var userId = Guid.NewGuid();

        var result = UserSearchRecord.Create(userId, "  orwell distopya ", "hybrid", 0, When);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.UserId.ShouldBe(userId);
        result.Data.Query.ShouldBe("orwell distopya"); // trim
        result.Data.Mode.ShouldBe("hybrid");
        result.Data.ResultCount.ShouldBe(0); // sonuçsuz arama da geçerli
        result.Data.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_BlankMode_DefaultsToKeyword()
    {
        var result = UserSearchRecord.Create(Guid.NewGuid(), "orwell", "", 3, When);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Mode.ShouldBe("keyword");
    }
}
