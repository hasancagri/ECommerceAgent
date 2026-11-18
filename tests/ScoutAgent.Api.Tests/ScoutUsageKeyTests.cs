using Shouldly;
using ScoutAgent.Api.RateLimiting;
using Xunit;

namespace ScoutAgent.Api.Tests;

public class ScoutUsageKeyTests
{
    [Fact]
    public void Partition_WithUserId_ReturnsUserKey() =>
        ScoutUsageKey.Partition(Guid.Parse("11111111-1111-1111-1111-111111111111"), "9.9.9.9")
            .ShouldBe("u:11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Partition_AnonymousWithIp_ReturnsIpKey() =>
        ScoutUsageKey.Partition(null, "9.9.9.9").ShouldBe("ip:9.9.9.9");

    [Fact]
    public void Partition_NoUserNoIp_ReturnsUnknown() =>
        ScoutUsageKey.Partition(null, null).ShouldBe("ip:unknown");
}
