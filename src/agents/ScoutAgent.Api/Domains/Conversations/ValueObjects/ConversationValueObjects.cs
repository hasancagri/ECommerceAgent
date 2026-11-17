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

    // 017: assistant turunda gösterilen yapılı tool sonuçları (kitap kartları) — opak JSON string
    // (`[{tool,result}]`, unwrap-sonrası; React ReplyItem[] olarak parse eder). User turunda null.
    // Stored VO'da JsonElement YOK (Marten record-VO deserialize tuzağı) — ham string tutulur. Bu
    // alan YALNIZ reload sadakati içindir; modele giden bağlama GİRMEZ (BuildContext yalnız Text okur).
    public string? ItemsJson { get; private set; }

    private ChatMessage()
    {
    }

    public static ChatMessage Create(ChatRole role, string text, DateTimeOffset createdAt,
        string? itemsJson = null) => new()
    {
        Role = role,
        Text = text,
        CreatedAt = createdAt,
        ItemsJson = itemsJson,
    };
}
