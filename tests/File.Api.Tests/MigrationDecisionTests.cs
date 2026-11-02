using FileApi.Migration;
using Shouldly;
using Xunit;

namespace File.Api.Tests;

public class MigrationDecisionTests
{
    [Fact]
    public void Exists_Skips()
    {
        MigrationDecision.Decide(exists: true, imageUrl: "http://x/a.jpg")
            .ShouldBe(CoverMigrationDecision.Skip);
    }

    [Fact]
    public void NotExists_WithImageUrl_Downloads()
    {
        MigrationDecision.Decide(exists: false, imageUrl: "http://x/a.jpg")
            .ShouldBe(CoverMigrationDecision.Download);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NotExists_BlankImageUrl_Skips(string? imageUrl)
    {
        MigrationDecision.Decide(exists: false, imageUrl: imageUrl)
            .ShouldBe(CoverMigrationDecision.Skip);
    }
}
