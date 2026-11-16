namespace Catalog.Api.Tests;

// 011 T006 (test-first, İlke VI): arama parametre validasyonu — saf. Boş sorgu hata değil (FR-003),
// limit guard (FR-004). SQL'e gitmeden handler bunları geçirir.
public class ProductSearchValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    [InlineData(null)]
    public void IsBlankQuery_EmptyOrWhitespace_ReturnsTrue(string? query)
    {
        SearchParameters.IsBlankQuery(query).ShouldBeTrue();
    }

    [Fact]
    public void IsBlankQuery_RealTerm_ReturnsFalse()
    {
        SearchParameters.IsBlankQuery("orwell").ShouldBeFalse();
    }

    [Fact]
    public void NormalizeQuery_CollapsesWhitespaceAndTrims()
    {
        SearchParameters.NormalizeQuery("  orwell   distopya ").ShouldBe("orwell distopya");
    }

    [Fact]
    public void NormalizeQuery_OverlongQuery_TruncatedToMax()
    {
        var longQuery = new string('a', SearchParameters.MaxQueryLength + 50);

        SearchParameters.NormalizeQuery(longQuery).Length.ShouldBe(SearchParameters.MaxQueryLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(null)]
    public void NormalizeLimit_ZeroNegativeOrNull_ReturnsDefault(int? limit)
    {
        SearchParameters.NormalizeLimit(limit).ShouldBe(SearchParameters.DefaultLimit);
    }

    [Fact]
    public void NormalizeLimit_AboveMax_ClampedToMax()
    {
        SearchParameters.NormalizeLimit(5000).ShouldBe(SearchParameters.MaxLimit);
    }

    [Fact]
    public void NormalizeLimit_WithinRange_Unchanged()
    {
        SearchParameters.NormalizeLimit(37).ShouldBe(37);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-3)]
    public void NormalizeOffset_NullOrNegative_ReturnsZero(int? offset)
    {
        SearchParameters.NormalizeOffset(offset).ShouldBe(0);
    }

    [Fact]
    public void NormalizeOffset_Positive_Unchanged()
    {
        SearchParameters.NormalizeOffset(40).ShouldBe(40);
    }
}
