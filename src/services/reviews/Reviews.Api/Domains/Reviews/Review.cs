namespace Reviews.Api.Domains.Reviews;

// satin-alma sartli urun yorumu. Yayin HEMEN (Visible dogar, FR-010); moderasyon async
// kosar ve ihlalde ApplyModeration gizler (FR-011). Teklik: Marten UniqueIndex(UserId, ProductId).
public class Review : AggregateRoot
{
    // Kontrat siniri (contracts/reviews-rest-api.md): metin en fazla 2000 karakter.
    public const int MaxTextLength = 2000;

    public Guid ProductId { get; private set; }
    public Guid UserId { get; private set; }
    public int Rating { get; private set; }
    public string? Text { get; private set; }

    // Ham gorunen ad (token claim'i); yuzeye ReviewerName VO'sunun Masked() ciktisi cikar (R7).
    public string ReviewerName { get; private set; } = null!;

    public ReviewStatus Status { get; private set; }

    // Hidden ise ihlal kategorisi + kisa gerekce (iz; yuzeye cikmaz).
    public string? ModerationCategory { get; private set; }
    public string? ModerationReason { get; private set; }

    // Denetim tamamlanma ani; null = denetim bekliyor (yorum yine gorunur — fail-open, FR-012).
    public DateTimeOffset? ModeratedAtUtc { get; private set; }

    private Review()
    {
    }

    /// <summary>Yeni yorumu Visible durumda olusturur; puan 1-5 tam, metin sinirli, ad zorunlu.</summary>
    public static ResultDomain<Review> Create(
        Guid productId, Guid userId, int rating, string? text,
        ValueObjects.ReviewerName reviewerName, DateTimeOffset now)
    {
        var messages = new List<MessageItem>();

        if (productId == Guid.Empty)
            messages.Add(new MessageItem { Property = nameof(ProductId), Code = ReviewsResourceConstants.REVIEW_REFERENCE_INVALID });

        if (userId == Guid.Empty)
            messages.Add(new MessageItem { Property = nameof(UserId), Code = ReviewsResourceConstants.REVIEW_REFERENCE_INVALID });

        if (rating is < 1 or > 5)
            messages.Add(new MessageItem { Property = nameof(Rating), Code = ReviewsResourceConstants.REVIEW_RATING_INVALID });

        if (text is not null && text.Length > MaxTextLength)
            messages.Add(new MessageItem { Property = nameof(Text), Code = ReviewsResourceConstants.REVIEW_TEXT_TOO_LONG });

        if (messages.Count > 0)
            return ResultDomain<Review>.Error(messages);

        return ResultDomain<Review>.Ok(new Review
        {
            ProductId = productId,
            UserId = userId,
            Rating = rating,
            Text = text,
            ReviewerName = reviewerName.Value,
            Status = ReviewStatus.Visible,
            CreatedTime = now.UtcDateTime,
        });
    }

    /// <summary>Moderasyon kararini uygular: ihlalde gizler (Hidden), temizde yalniz damgalar; tekrar no-op.</summary>
    public ResultDomain ApplyModeration(ValueObjects.ModerationVerdict verdict, DateTimeOffset now)
    {
        // At-least-once teslimat: denetim tamamlanmissa ikinci karar durumu degistirmez (idempotent).
        if (ModeratedAtUtc is not null)
            return ResultDomain.Ok();

        if (verdict.Violation)
        {
            Status = ReviewStatus.Hidden;
            ModerationCategory = verdict.Category;
            ModerationReason = verdict.Reason;
        }

        ModeratedAtUtc = now;
        return ResultDomain.Ok();
    }
}

// yorum durumu — Hidden terminaldir (itiraz/geri alma v1 disi).
public enum ReviewStatus
{
    Visible = 1,
    Hidden = 2
}