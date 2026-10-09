using Discount.Api.Constants;

namespace Discount.Api.Tests;

// 079 İLKE VI: Campaign aggregate davranışı test-first. Create invariant'ları + Cancel/IsEffectiveAt.
public class CampaignTests
{
    private static readonly DateTime Now = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Ref = Guid.NewGuid();

    [Fact]
    public void Create_valid_active_now_succeeds()
    {
        var r = Campaign.Create("Roman Bahar", ScopeType.Category, Ref, 20, Now, Now.AddDays(7), Now);

        r.IsSuccess.ShouldBeTrue();
        r.Data!.Percentage.ShouldBe(20);
        r.Data.Status.ShouldBe(CampaignStatus.Active);
        r.Data.IsEffectiveAt(Now).ShouldBeTrue();
    }

    [Fact]
    public void Create_FutureStart_IsScheduledAndNotEffective()
    {
        var r = Campaign.Create("İleri", ScopeType.Category, Ref, 15, Now.AddDays(1), Now.AddDays(3), Now);

        r.IsSuccess.ShouldBeTrue();
        r.Data!.Status.ShouldBe(CampaignStatus.Scheduled);
        r.Data.IsEffectiveAt(Now).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(150)]
    [InlineData(-5)]
    public void Create_PercentageOutOfRange_ReturnsError(int pct)
    {
        var r = Campaign.Create("X", ScopeType.Category, Ref, pct, Now, Now.AddDays(1), Now);

        r.IsSuccess.ShouldBeFalse();
        r.Messages![0].Code.ShouldBe(DiscountResourceConstants.CAMPAIGN_PERCENTAGE_INVALID);
    }

    [Fact]
    public void Create_EndBeforeStart_ReturnsError()
    {
        var r = Campaign.Create("X", ScopeType.Category, Ref, 20, Now, Now.AddDays(-1), Now);

        r.IsSuccess.ShouldBeFalse();
        r.Messages![0].Code.ShouldBe(DiscountResourceConstants.CAMPAIGN_WINDOW_INVALID);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyName_ReturnsError(string name)
    {
        var r = Campaign.Create(name, ScopeType.Category, Ref, 20, Now, Now.AddDays(1), Now);

        r.IsSuccess.ShouldBeFalse();
        r.Messages![0].Code.ShouldBe(DiscountResourceConstants.CAMPAIGN_NAME_REQUIRED);
    }

    [Fact]
    public void Create_EmptyScopeRef_ReturnsError()
    {
        var r = Campaign.Create("X", ScopeType.Category, Guid.Empty, 20, Now, Now.AddDays(1), Now);

        r.IsSuccess.ShouldBeFalse();
        r.Messages![0].Code.ShouldBe(DiscountResourceConstants.CAMPAIGN_SCOPE_REF_REQUIRED);
    }

    [Fact]
    public void Create_UndefinedScopeType_ReturnsError()
    {
        var r = Campaign.Create("X", (ScopeType)99, Ref, 20, Now, Now.AddDays(1), Now);

        r.IsSuccess.ShouldBeFalse();
        r.Messages![0].Code.ShouldBe(DiscountResourceConstants.CAMPAIGN_SCOPE_TYPE_INVALID);
    }

    [Fact]
    public void Create_NullEnd_OpenEnded()
    {
        var r = Campaign.Create("Süresiz", ScopeType.Author, Ref, 10, Now, null, Now);

        r.IsSuccess.ShouldBeTrue();
        r.Data!.IsEffectiveAt(Now.AddYears(5)).ShouldBeTrue();
    }

    [Fact]
    public void Cancel_FromActive_MakesIneffective()
    {
        var campaign = Campaign.Create("X", ScopeType.Category, Ref, 20, Now, Now.AddDays(7), Now).Data!;

        var cancel = campaign.Cancel();

        cancel.IsSuccess.ShouldBeTrue();
        campaign.Status.ShouldBe(CampaignStatus.Cancelled);
        campaign.IsEffectiveAt(Now).ShouldBeFalse();
    }

    [Fact]
    public void Cancel_twice_errors()
    {
        var campaign = Campaign.Create("X", ScopeType.Category, Ref, 20, Now, Now.AddDays(7), Now).Data!;
        campaign.Cancel();

        var again = campaign.Cancel();

        again.IsSuccess.ShouldBeFalse();
        again.Messages![0].Code.ShouldBe(DiscountResourceConstants.CAMPAIGN_ALREADY_CANCELLED);
    }
}
