namespace ScoutAgent.Api.Domains.Conversations;

// 023: sohbet cevap tipleri — eski SendMessage.{Response,Reply,ReplyItem}'ten TAŞINDI (/messages
// emekliliği). SearchTyped (022 /search) kullanır; React aynı {conversationId, reply:{text, items}}
// şeklini parse eder (camelCase). ReplyItem.Result tool dönüşünden BİREBİR (uydurma 0).
public record ChatReplyItem(string Tool, object Result, object? Filters = null);

public class ChatReply
{
    public string Text { get; set; } = string.Empty;
    public IReadOnlyList<ChatReplyItem> Items { get; set; } = [];
}

public class ChatResponse
{
    public Guid ConversationId { get; set; }
    public string? Title { get; set; }
    public ChatReply Reply { get; set; } = new();
}
