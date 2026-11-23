using ScoutAgent.Api.Agents;

namespace ScoutAgent.Api.Tests;

public class PaginationIntentTests
{
    [Theory]
    [InlineData("devamını getir")]
    [InlineData("Devamını Getir")]
    [InlineData("DEVAMINI GETİR")]
    [InlineData("diğer sonuçları getir")]
    [InlineData("sonraki")]
    [InlineData("daha fazla göster")]
    [InlineData("devamını getirir misin")]
    public void IsMore_PaginationPhrase_ReturnsTrue(string msg) =>
        PaginationIntent.IsMore(msg).ShouldBeTrue();

    [Theory]
    [InlineData("Can Yayınları'ndan roman")]
    [InlineData("tarihi kitaplar")]
    [InlineData("")]
    [InlineData(null)]
    public void IsMore_NonPaginationOrEmpty_ReturnsFalse(string? msg) =>
        PaginationIntent.IsMore(msg!).ShouldBeFalse();
}
