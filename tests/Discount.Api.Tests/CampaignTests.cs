namespace Discount.Api.Tests;

// 003: Campaign davranış testleri — test-first (İlke VI). Zaman DIŞARIDAN verilir (IsActiveAt),
// domain saat okumaz. Oran %1-%90; indirim liste fiyatını EZMEZ (ayrı kavram).
public class CampaignTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static Campaign NewCampaign(decimal rate = 0.20m) =>
        Campaign.Create(Guid.NewGuid(), rate, Now, Now.AddDays(7)).Data!;

    // --- Create ---

    [Fact]
    public void Create_ValidInput_SetsFields()
    {
        var productId = Guid.NewGuid();

        var result = Campaign.Create(productId, 0.25m, Now, Now.AddDays(3));

        result.IsSuccess.ShouldBeTrue();
        var c = result.Data!;
        c.ProductId.ShouldBe(productId);
        c.Rate.ShouldBe(0.25m);
        c.StartsAt.ShouldBe(Now);
        c.EndsAt.ShouldBe(Now.AddDays(3));
        c.IsCancelled.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(0.91)]
    [InlineData(1.5)]
    public void Create_RateOutOfRange_ReturnsError(decimal rate)
    {
        var result = Campaign.Create(Guid.NewGuid(), rate, Now, Now.AddDays(1));

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == DiscountResourceConstants.CAMPAIGN_RATE_INVALID);
    }

    [Fact]
    public void Create_EndBeforeStart_ReturnsError()
    {
        var result = Campaign.Create(Guid.NewGuid(), 0.2m, Now, Now.AddMinutes(-1));

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == DiscountResourceConstants.CAMPAIGN_WINDOW_INVALID);
    }

    [Fact]
    public void Create_EmptyProductId_ReturnsError()
    {
        var result = Campaign.Create(Guid.Empty, 0.2m, Now, Now.AddDays(1));

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == DiscountResourceConstants.CAMPAIGN_PRODUCT_REQUIRED);
    }

    // --- Cancel ---

    [Fact]
    public void Cancel_ActiveCampaign_Deactivates()
    {
        var c = NewCampaign();

        c.Cancel().IsSuccess.ShouldBeTrue();

        c.IsCancelled.ShouldBeTrue();
        c.IsActiveAt(Now.AddHours(1)).ShouldBeFalse();
    }

    // --- IsActiveAt (saf karar) ---

    [Fact]
    public void IsActiveAt_WithinWindow_ReturnsTrue()
    {
        NewCampaign().IsActiveAt(Now.AddDays(1)).ShouldBeTrue();
    }

    [Fact]
    public void IsActiveAt_BeforeStart_ReturnsFalse()
    {
        NewCampaign().IsActiveAt(Now.AddMinutes(-1)).ShouldBeFalse();
    }

    [Fact]
    public void IsActiveAt_AfterEnd_ReturnsFalse()
    {
        NewCampaign().IsActiveAt(Now.AddDays(8)).ShouldBeFalse();
    }

    [Fact]
    public void IsActiveAt_WhenCancelled_ReturnsFalse()
    {
        var c = NewCampaign();
        c.Cancel();

        c.IsActiveAt(Now.AddDays(1)).ShouldBeFalse();
    }
}
