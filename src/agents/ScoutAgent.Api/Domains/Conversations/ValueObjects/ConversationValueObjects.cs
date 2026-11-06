namespace ScoutAgent.Api.Domains.Conversations.ValueObjects;

// Sohbetteki tek mesaj rolü. (Yapılı tool öğeleri transkripte GİRMEZ — bağlam zehirlenmesini önler,
// data-model.md; cevap anında tool'dan üretilir, burada yalnız metin tutulur.)
public enum ChatRole
{
    User = 0,
    Assistant = 1,
}

// ChatMessage VO: record + private parametresiz ctor + private setter (Marten record VO tuzağı —
// saklanan record okuma anında parametresiz ctor ister; yoksa deserialize 500).
public record ChatMessage
{
    public ChatRole Role { get; private set; }
    public string Text { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }

    private ChatMessage()
    {
    }

    public static ChatMessage Create(ChatRole role, string text, DateTimeOffset createdAt) => new()
    {
        Role = role,
        Text = text,
        CreatedAt = createdAt,
    };
}
