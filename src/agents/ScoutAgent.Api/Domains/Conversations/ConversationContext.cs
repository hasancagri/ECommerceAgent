namespace ScoutAgent.Api.Domains.Conversations;

// Modele giden bağlam rolü (sohbet mesaj rolünden ayrı — özet "system" olarak başa girer).
public enum ContextRole
{
    System = 0,
    User = 1,
    Assistant = 2,
}

public record ContextMessage(ContextRole Role, string Text);

// 007 (İlke VI test-first): saf bağlam kurucu. (Summary, Messages, WindowSize) → modele verilecek
// sıralı bağlam. Marten belgesi DEĞİL, saf fonksiyon. Kural: özet varsa başa system olarak eklenir;
// yalnız son WindowSize ham mesaj taşınır (sohbet uzadıkça bağlam maliyeti sabit); hacimli tool
// sonuçları zaten transkriptte yok (ChatMessage sadece metin tutar) → bağlama ham gömülmez.
public static class ConversationContext
{
    public static IReadOnlyList<ContextMessage> Build(
        string? summary, IReadOnlyList<ChatMessage> messages, int windowSize)
    {
        var result = new List<ContextMessage>();

        if (!string.IsNullOrWhiteSpace(summary))
            result.Add(new ContextMessage(ContextRole.System, summary.Trim()));

        var window = windowSize < 0 ? 0 : windowSize;
        var skip = messages.Count > window ? messages.Count - window : 0;

        for (var i = skip; i < messages.Count; i++)
        {
            var m = messages[i];
            var role = m.Role == ChatRole.User ? ContextRole.User : ContextRole.Assistant;
            result.Add(new ContextMessage(role, m.Text));
        }

        return result;
    }
}
