namespace Discount.Api.Tests;

// 079 İLKE VI: scheduled-fire GUARD çekirdeği (idempotency temeli). Handler'ın "bayat/iptal → no-op"
// kararını süren pür predicate = Campaign.IsEffectiveAt + MarkActive/MarkEnded geçişleri. Handler'ın
// Marten/broker teli test-sonra (quickstart canlı doğrulama).
public class CampaignScheduleTests
{
    private static readonly DateTime Now = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsEffectiveAt_Cancelled_ReturnsFalse()
    {
        var c = Campaign.Create("X", ScopeType.Category, Guid.NewGuid(), 20, Now, Now.AddDays(7), Now).Data!;
        c.Cancel();

        // start-fire geç gelse bile guard: iptal → uygulanmaz.
        c.IsEffectiveAt(Now).ShouldBeFalse();
    }

    [Fact]
    public void Past_window_is_not_effective_stale_fire_guard()
    {
        var c = Campaign.Create("X", ScopeType.Category, Guid.NewGuid(), 20, Now, Now.AddHours(1), Now).Data!;

        // now pencere dışına çıkınca (bayat start-fire) guard uygulamayı engeller.
        c.IsEffectiveAt(Now.AddHours(2)).ShouldBeFalse();
    }

    [Fact]
    public void MarkActive_is_idempotent_and_skips_cancelled()
    {
        var c = Campaign.Create("X", ScopeType.Category, Guid.NewGuid(), 20, Now.AddDays(1), Now.AddDays(3), Now).Data!;
        c.Status.ShouldBe(CampaignStatus.Scheduled);

        c.MarkActive();
        c.Status.ShouldBe(CampaignStatus.Active);

        c.Cancel();
        c.MarkActive(); // iptal sonrası aktifleşme yok.
        c.Status.ShouldBe(CampaignStatus.Cancelled);
    }

    [Fact]
    public void MarkEnded_sets_ended_but_never_overrides_cancelled()
    {
        var c = Campaign.Create("X", ScopeType.Category, Guid.NewGuid(), 20, Now, Now.AddDays(1), Now).Data!;
        c.MarkEnded();
        c.Status.ShouldBe(CampaignStatus.Ended);

        var c2 = Campaign.Create("Y", ScopeType.Category, Guid.NewGuid(), 20, Now, Now.AddDays(1), Now).Data!;
        c2.Cancel();
        c2.MarkEnded();
        c2.Status.ShouldBe(CampaignStatus.Cancelled);
    }
}
