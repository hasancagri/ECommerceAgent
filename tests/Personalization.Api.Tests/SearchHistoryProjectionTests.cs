namespace Personalization.Api.Tests;

// 013 T006 (test-first, İlke VI): SearchHistoryProjection — ham UserSearchRecord listesini
// tekilleştirilmiş SearchHistoryEntry görünümüne indirger (normalize-grupla, en son mod/zaman,
// LastSearchedAt azalan) + Türkçe-duyarlı Normalize. Saf (host'suz).
public class SearchHistoryProjectionTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    private static UserSearchRecord Rec(string query, string mode, int resultCount, DateTimeOffset at)
        => UserSearchRecord.Create(User, query, mode, resultCount, at).Data!;

    [Fact]
    public void Deduplicate_RepeatedQuery_ReturnsSingleEntry()
    {
        var records = new[]
        {
            Rec("dostoyevski", "keyword", 20, T0),
            Rec("dostoyevski", "keyword", 20, T0.AddMinutes(1)),
            Rec("dostoyevski", "keyword", 5, T0.AddMinutes(2)),
        };

        var result = SearchHistoryProjection.Deduplicate(records);

        result.Count.ShouldBe(1);
        result[0].Query.ShouldBe("dostoyevski");
        result[0].Count.ShouldBe(3);
    }

    [Fact]
    public void Deduplicate_TurkishCaseVariants_GroupAsOne()
    {
        var records = new[]
        {
            Rec("Dostoyevski", "keyword", 20, T0),
            Rec("dostoyevski", "keyword", 20, T0.AddMinutes(1)),
            Rec("İSTANBUL", "keyword", 3, T0.AddMinutes(2)),
            Rec("istanbul", "keyword", 3, T0.AddMinutes(3)),
        };

        var result = SearchHistoryProjection.Deduplicate(records);

        result.Count.ShouldBe(2);
        result.ShouldContain(e => e.Count == 2); // her grup 2 ham kayıt
    }

    [Fact]
    public void Deduplicate_PicksLatestModeAndTime()
    {
        var records = new[]
        {
            Rec("kafka", "keyword", 10, T0),
            Rec("kafka", "hybrid", 8, T0.AddMinutes(5)),
        };

        var result = SearchHistoryProjection.Deduplicate(records);

        result.Count.ShouldBe(1);
        result[0].Mode.ShouldBe("hybrid");                    // en son aramanın modu
        result[0].LastSearchedAt.ShouldBe(T0.AddMinutes(5));  // en büyük zaman
    }

    [Fact]
    public void Deduplicate_SortsByLastSearchedDescending()
    {
        var records = new[]
        {
            Rec("eski", "keyword", 1, T0),
            Rec("yeni", "keyword", 1, T0.AddHours(1)),
        };

        var result = SearchHistoryProjection.Deduplicate(records);

        result[0].Query.ShouldBe("yeni");  // en yeni üstte
        result[1].Query.ShouldBe("eski");
    }

    [Fact]
    public void Deduplicate_EmptyInput_ReturnsEmpty()
    {
        var result = SearchHistoryProjection.Deduplicate(Array.Empty<UserSearchRecord>());

        result.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Dostoyevski", "dostoyevski")]
    [InlineData("İSTANBUL", "istanbul")]
    [InlineData("  kafka  ", "kafka")]
    [InlineData("harf   boşluk", "harf boşluk")]
    public void Normalize_FoldsTurkishIAndCaseAndWhitespace(string a, string b)
    {
        SearchHistoryProjection.Normalize(a).ShouldBe(SearchHistoryProjection.Normalize(b));
    }
}
