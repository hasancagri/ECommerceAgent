namespace ScoutAgent.Api.Tests;

// 017: kullanıcı-tetikli yeniden adlandırma — guard'lar + UpdatedTime değişmezliği.
public class ConversationRenameTests
{
    private static Conversation New() => Conversation.Start(Guid.NewGuid(), "ilk mesaj").Data!;

    [Fact]
    public void Rename_EmptyTitle_ReturnsError()
    {
        var convo = New();

        convo.Rename("   ").IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Rename_Trims()
    {
        var convo = New();

        convo.Rename("  Yeni Başlık  ");

        convo.Title.ShouldBe("Yeni Başlık");
    }

    [Fact]
    public void Rename_TooLong_ReturnsError()
    {
        var convo = New();
        var tooLong = new string('a', Conversation.MaxTitleLength + 1);

        convo.Rename(tooLong).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Rename_ValidTitle_SetsTitle()
    {
        var convo = New();

        var result = convo.Rename("Fantastik kitaplar");

        result.IsSuccess.ShouldBeTrue();
        convo.Title.ShouldBe("Fantastik kitaplar");
    }

    [Fact]
    public void Rename_DoesNotChangeUpdatedTime()
    {
        var convo = New();
        // Start sonrası bir aktivite ile UpdatedTime'ı belirle.
        convo.AddAssistantMessage("cevap");
        var before = convo.UpdatedTime;

        convo.Rename("Elle verilen başlık");

        convo.UpdatedTime.ShouldBe(before);
    }
}
