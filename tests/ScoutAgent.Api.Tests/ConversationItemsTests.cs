using ScoutAgent.Api.Domains.Conversations.ValueObjects;

namespace ScoutAgent.Api.Tests;

// 017: assistant mesajında yapılı kart (ItemsJson) saklama — reload sadakati.
public class ConversationItemsTests
{
    [Fact]
    public void Create_WithItemsJson_StoresItems()
    {
        var json = "[{\"tool\":\"search_books\",\"result\":{\"items\":[]}}]";

        var msg = ChatMessage.Create(ChatRole.Assistant, "işte kitaplar", DateTimeOffset.UtcNow, json);

        msg.ItemsJson.ShouldBe(json);
    }

    [Fact]
    public void Create_WithoutItemsJson_ItemsNull()
    {
        var msg = ChatMessage.Create(ChatRole.User, "merhaba", DateTimeOffset.UtcNow);

        msg.ItemsJson.ShouldBeNull();
    }

    [Fact]
    public void AddAssistantMessage_WithItems_StoresItems()
    {
        var convo = Conversation.Start(Guid.NewGuid(), "popüler romanlar").Data!;
        var json = "[{\"tool\":\"search_books\",\"result\":{\"items\":[{\"name\":\"X\"}]}}]";

        var result = convo.AddAssistantMessage("buyur", json);

        result.IsSuccess.ShouldBeTrue();
        var last = convo.Messages[^1];
        last.Role.ShouldBe(ChatRole.Assistant);
        last.ItemsJson.ShouldBe(json);
    }

    [Fact]
    public void AddAssistantMessage_WithoutItems_ItemsNull()
    {
        var convo = Conversation.Start(Guid.NewGuid(), "selam").Data!;

        convo.AddAssistantMessage("düz cevap");

        convo.Messages[^1].ItemsJson.ShouldBeNull();
    }

    [Fact]
    public void AddAssistantMessage_EmptyText_ReturnsError()
    {
        var convo = Conversation.Start(Guid.NewGuid(), "selam").Data!;

        var result = convo.AddAssistantMessage("   ", "[]");

        result.IsSuccess.ShouldBeFalse();
    }
}
