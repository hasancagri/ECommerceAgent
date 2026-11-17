using Shouldly;
using ScoutAgent.Api.Domains.Conversations;
using ScoutAgent.Api.Domains.Conversations.ValueObjects;
using Xunit;

namespace ScoutAgent.Api.Tests;

public class ConversationTests
{
    [Fact]
    public void Start_EmptyUser_ReturnsError()
    {
        var result = Conversation.Start(Guid.Empty, "Nutuk var mı?");

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Start_EmptyFirstMessage_ReturnsError()
    {
        var result = Conversation.Start(Guid.NewGuid(), "   ");

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Start_ValidInput_SetsTitleAndFirstUserMessage()
    {
        var userId = Guid.NewGuid();

        var result = Conversation.Start(userId, "Sabahattin Ali kitapları");

        result.IsSuccess.ShouldBeTrue();
        var c = result.Data!;
        c.UserId.ShouldBe(userId);
        c.Title.ShouldBe("Sabahattin Ali kitapları");
        c.Messages.Count.ShouldBe(1);
        c.Messages[0].Role.ShouldBe(ChatRole.User);
        c.Messages[0].Text.ShouldBe("Sabahattin Ali kitapları");
    }

    [Fact]
    public void Start_LongFirstMessage_TruncatesTitle()
    {
        var longText = new string('a', 120);

        var result = Conversation.Start(Guid.NewGuid(), longText);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Title.Length.ShouldBeLessThanOrEqualTo(61); // 60 + "…"
        result.Data!.Title.ShouldEndWith("…");
    }

    [Fact]
    public void AddUserMessage_ValidText_AppendsUserMessage()
    {
        var c = Conversation.Start(Guid.NewGuid(), "ilk").Data!;

        var result = c.AddUserMessage("ikinci soru");

        result.IsSuccess.ShouldBeTrue();
        c.Messages.Count.ShouldBe(2);
        c.Messages[1].Role.ShouldBe(ChatRole.User);
        c.Messages[1].Text.ShouldBe("ikinci soru");
    }

    [Fact]
    public void AddAssistantMessage_Empty_ReturnsError()
    {
        var c = Conversation.Start(Guid.NewGuid(), "ilk").Data!;

        var result = c.AddAssistantMessage("");

        result.IsSuccess.ShouldBeFalse();
        c.Messages.Count.ShouldBe(1);
    }

    [Fact]
    public void AddAssistantMessage_ValidText_AppendsAssistantMessage()
    {
        var c = Conversation.Start(Guid.NewGuid(), "ilk").Data!;

        c.AddAssistantMessage("Kâşif cevabı").IsSuccess.ShouldBeTrue();

        c.Messages[^1].Role.ShouldBe(ChatRole.Assistant);
        c.Messages[^1].Text.ShouldBe("Kâşif cevabı");
    }

    [Fact]
    public void ApplySummary_ValidInput_SetsSummaryAndAdvancesSummarizedUpTo()
    {
        var c = Conversation.Start(Guid.NewGuid(), "ilk").Data!;
        c.AddAssistantMessage("cevap");

        var result = c.ApplySummary("Sohbet özeti", 2);

        result.IsSuccess.ShouldBeTrue();
        c.Summary.ShouldBe("Sohbet özeti");
        c.SummarizedUpTo.ShouldBe(2);
    }

    [Fact]
    public void ApplySummary_IndexBeforeExisting_ReturnsError()
    {
        var c = Conversation.Start(Guid.NewGuid(), "ilk").Data!;
        c.AddAssistantMessage("cevap");
        c.ApplySummary("özet", 2);

        var result = c.ApplySummary("yeni özet", 1); // geriye gidemez

        result.IsSuccess.ShouldBeFalse();
        c.SummarizedUpTo.ShouldBe(2);
    }

    [Fact]
    public void ApplySummary_IndexBeyondMessages_ReturnsError()
    {
        var c = Conversation.Start(Guid.NewGuid(), "ilk").Data!;

        var result = c.ApplySummary("özet", 5); // mesaj sayısını aşar

        result.IsSuccess.ShouldBeFalse();
    }
}
