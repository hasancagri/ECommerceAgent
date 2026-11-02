namespace Discount.Api.Tests;

// 003: ürün-başına etkin oran seçimi — saf helper (süreç bunu çağırır). Son-gelen-kazanır (079 kuralı).
public class CampaignSelectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static Campaign At(decimal rate, DateTimeOffset createdAt)
    {
        var c = Campaign.Create(Guid.NewGuid(), rate, Now.AddDays(-1), Now.AddDays(7)).Data!;
        c.CreatedTime = createdAt.UtcDateTime; // AggregateRoot denetim alanı — oluşturma sırası
        return c;
    }

    [Fact]
    public void ResolveActiveRate_NoActiveCampaign_ReturnsNull()
    {
        CampaignSelection.ResolveActiveRate(new List<Campaign>(), Now).ShouldBeNull();
    }

    [Fact]
    public void ResolveActiveRate_SingleActive_ReturnsRate()
    {
        var c = At(0.20m, Now.AddHours(-2));

        CampaignSelection.ResolveActiveRate(new[] { c }, Now).ShouldBe(0.20m);
    }

    [Fact]
    public void ResolveActiveRate_Overlapping_LastCreatedWins()
    {
        var older = At(0.20m, Now.AddHours(-5));
        var newer = At(0.30m, Now.AddHours(-1));

        CampaignSelection.ResolveActiveRate(new[] { older, newer }, Now).ShouldBe(0.30m);
    }

    [Fact]
    public void ResolveActiveRate_ActiveButCancelled_ReturnsNull()
    {
        var c = At(0.20m, Now.AddHours(-1));
        c.Cancel();

        CampaignSelection.ResolveActiveRate(new[] { c }, Now).ShouldBeNull();
    }
}
