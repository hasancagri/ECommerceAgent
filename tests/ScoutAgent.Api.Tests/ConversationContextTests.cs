using Shouldly;
using ScoutAgent.Api.Domains.Conversations;
using ScoutAgent.Api.Domains.Conversations.ValueObjects;
using Xunit;

namespace ScoutAgent.Api.Tests;

public class ConversationContextTests
{
    private static IReadOnlyList<ChatMessage> Messages(int count)
    {
        var list = new List<ChatMessage>();
        for (var i = 0; i < count; i++)
        {
            var role = i % 2 == 0 ? ChatRole.User : ChatRole.Assistant;
            list.Add(ChatMessage.Create(role, $"m{i}", DateTimeOffset.UtcNow));
        }

        return list;
    }

    [Fact]
    public void Build_WithSummary_PrependsSummaryAsSystem()
    {
        var ctx = ConversationContext.Build("damıtılmış özet", Messages(2), windowSize: 10);

        ctx[0].Role.ShouldBe(ContextRole.System);
        ctx[0].Text.ShouldBe("damıtılmış özet");
    }

    [Fact]
    public void Build_NoSummary_OmitsSystemMessage()
    {
        var ctx = ConversationContext.Build(null, Messages(2), windowSize: 10);

        ctx.ShouldAllBe(m => m.Role != ContextRole.System);
        ctx.Count.ShouldBe(2);
    }

    [Fact]
    public void Build_MoreThanWindow_TrimsToMostRecent()
    {
        var ctx = ConversationContext.Build(null, Messages(30), windowSize: 10);

        ctx.Count.ShouldBe(10);
        ctx[0].Text.ShouldBe("m20"); // son 10: m20..m29
        ctx[^1].Text.ShouldBe("m29");
    }

    [Fact]
    public void Build_FewerThanWindow_ReturnsAllInOrder()
    {
        var ctx = ConversationContext.Build(null, Messages(3), windowSize: 10);

        ctx.Count.ShouldBe(3);
        ctx[0].Text.ShouldBe("m0");
        ctx[2].Text.ShouldBe("m2");
    }

    [Fact]
    public void Build_WithSummaryAndWindow_SummaryFirstThenRecent()
    {
        var ctx = ConversationContext.Build("özet", Messages(30), windowSize: 5);

        ctx.Count.ShouldBe(6); // 1 özet + son 5
        ctx[0].Role.ShouldBe(ContextRole.System);
        ctx[1].Text.ShouldBe("m25");
        ctx[^1].Text.ShouldBe("m29");
    }

    [Fact]
    public void Build_MapsRolesToContextRoles()
    {
        var ctx = ConversationContext.Build(null, Messages(2), windowSize: 10);

        ctx[0].Role.ShouldBe(ContextRole.User);       // m0
        ctx[1].Role.ShouldBe(ContextRole.Assistant);  // m1
    }
}
