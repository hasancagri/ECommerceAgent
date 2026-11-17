namespace ScoutAgent.Api.Domains.Conversations;

/// <summary>
/// Sohbet — ScoutAgent'ın KENDİ kök aggregate'i (İlke I: kendi verisi, kendi scoutDb'si). Girişli
/// kullanıcıya ait bir sohbet turu dizisi + damıtılmış özet. Invariant: UserId doğumda sabit, Messages
/// yalnız davranış metotlarından büyür (İlke II). Anonim sohbet KALICI DEĞİL — bu aggregate hiç yazılmaz.
/// </summary>
public class Conversation : AggregateRoot
{
    // 017: kullanıcı-tetikli yeniden adlandırmada başlık üst sınırı (domain sabiti — aggregate config
    // okumaz; DeriveTitle'ın 60 türetim kırpımından ayrı, elle rename için daha geniş tavan).
    public const int MaxTitleLength = 100;

    public Guid UserId { get; private set; }
    public string Title { get; private set; } = default!;

    private readonly List<ChatMessage> _messages = new();
    public IReadOnlyList<ChatMessage> Messages => _messages;

    // Eskiler damıtıldıkça dolan rolling özet + özete dahil edilmiş son mesaj indeksi (çift-özet engeli).
    public string? Summary { get; private set; }
    public int SummarizedUpTo { get; private set; }

    private Conversation()
    {
    }

    /// <summary>Yeni sohbet başlatır: Title ilk kullanıcı mesajından türetilir, ilk user mesajı eklenir.
    /// UserId boş ya da ilk mesaj boşsa hata.</summary>
    public static ResultDomain<Conversation> Start(Guid userId, string firstMessage)
    {
        if (userId == Guid.Empty)
            return ResultDomain<Conversation>.Error(new MessageItem
            { Property = nameof(UserId), Code = ScoutResourceConstants.CONVERSATION_USER_REQUIRED });
        if (string.IsNullOrWhiteSpace(firstMessage))
            return ResultDomain<Conversation>.Error(new MessageItem
            { Property = nameof(firstMessage), Code = ScoutResourceConstants.CONVERSATION_MESSAGE_EMPTY });

        var conversation = new Conversation
        {
            UserId = userId,
            Title = DeriveTitle(firstMessage),
        };
        conversation._messages.Add(ChatMessage.Create(ChatRole.User, firstMessage.Trim(), DateTime.UtcNow));
        return ResultDomain<Conversation>.Ok(conversation);
    }

    /// <summary>Akıllı başlığı atar (014 — ilk turda LLM üretir). Boş gelirse dokunmaz.</summary>
    public ResultDomain SetTitle(string title)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title.Trim();
            UpdatedTime = DateTime.UtcNow;
        }
        return ResultDomain.Ok();
    }

    /// <summary>Kullanıcı başlığı elle değiştirir (017). Boş/yalnız-boşluk reddedilir; trim'lenir;
    /// MaxTitleLength aşılırsa reddedilir. SetTitle'dan FARKI: UpdatedTime'a DOKUNMAZ — yeniden
    /// adlandırma sohbeti sidebar sırasında başa atmaz (başlık düzeltmesi "etkinlik" değildir).</summary>
    public ResultDomain Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(title), Code = ScoutResourceConstants.CONVERSATION_TITLE_EMPTY });

        var trimmed = title.Trim();
        if (trimmed.Length > MaxTitleLength)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(title), Code = ScoutResourceConstants.CONVERSATION_TITLE_TOO_LONG });

        Title = trimmed;
        return ResultDomain.Ok();
    }

    /// <summary>Kullanıcı mesajını ekler. Boş metin reddedilir.</summary>
    public ResultDomain AddUserMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(text), Code = ScoutResourceConstants.CONVERSATION_MESSAGE_EMPTY });
        _messages.Add(ChatMessage.Create(ChatRole.User, text.Trim(), DateTime.UtcNow));
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Kâşif (assistant) cevabını ekler. Boş metin reddedilir. 017: opsiyonel itemsJson —
    /// turda gösterilen yapılı kartlar (opak JSON) mesaja saklanır (reload sadakati); bağlama girmez.</summary>
    public ResultDomain AddAssistantMessage(string text, string? itemsJson = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(text), Code = ScoutResourceConstants.CONVERSATION_MESSAGE_EMPTY });
        _messages.Add(ChatMessage.Create(ChatRole.Assistant, text.Trim(), DateTime.UtcNow, itemsJson));
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Rolling özeti günceller ve SummarizedUpTo'yu ilerletir (çift-özetleme engeli). Boş özet
    /// reddedilir; upToIndex geriye gidemez ve mesaj sayısını aşamaz.</summary>
    public ResultDomain ApplySummary(string summary, int upToIndex)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(summary), Code = ScoutResourceConstants.VALUE_EMPTY });
        if (upToIndex < SummarizedUpTo || upToIndex > _messages.Count)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(upToIndex), Code = ScoutResourceConstants.VALUE_EMPTY });

        Summary = summary.Trim();
        SummarizedUpTo = upToIndex;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    // Başlık ilk mesajdan: tek satır, kırpılmış, en fazla 60 karakter (invariant değil, türetim).
    private static string DeriveTitle(string firstMessage)
    {
        var single = firstMessage.Trim().ReplaceLineEndings(" ");
        return single.Length <= 60 ? single : single[..60].TrimEnd() + "…";
    }
}
