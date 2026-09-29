namespace Reviews.Api.Domains.Reviews.ValueObjects;

// R7: yorum sahibinin gorunen adi. HAM deger saklanir; yuzeye YALNIZ Masked() cikar —
// maske kurali degisirse gecmis yorumlar yeniden maskelenebilir (goruntuleme kurali, veri degil).
public record ReviewerName
{
    public string Value { get; private init; } = default!;

    private ReviewerName() { }

    public static ResultDomain<ReviewerName> Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ResultDomain<ReviewerName>.Error(
                new MessageItem { Property = nameof(Value), Code = ReviewsResourceConstants.REVIEW_NAME_REQUIRED });

        return ResultDomain<ReviewerName>.Ok(new ReviewerName { Value = raw.Trim() });
    }

    // Her kelimenin ilk harfi + "**" ("Hasan Demiriz" → "H** D**"); tek harfli kelime oldugu gibi kalir.
    public string Masked() =>
        string.Join(" ", Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => word.Length == 1 ? word : $"{word[0]}**"));
}

// R5: moderasyon karari — agent yalniz KARAR verir, uygulama Review.ApplyModeration'da.
// Guard: violation=true iken gercek bir kategori zorunlu ("none"/bos kabul edilmez).
public record ModerationVerdict
{
    public bool Violation { get; private init; }
    public string Category { get; private init; } = default!;
    public string Reason { get; private init; } = default!;

    private ModerationVerdict() { }

    public static ResultDomain<ModerationVerdict> Create(bool violation, string category, string reason)
    {
        if (violation && (string.IsNullOrWhiteSpace(category)
                          || string.Equals(category, "none", StringComparison.OrdinalIgnoreCase)))
            return ResultDomain<ModerationVerdict>.Error(new MessageItem
            {
                Property = nameof(Category),
                Code = ReviewsResourceConstants.REVIEW_MODERATION_VERDICT_INVALID
            });

        return ResultDomain<ModerationVerdict>.Ok(new ModerationVerdict
        {
            Violation = violation,
            Category = violation ? category.Trim() : "none",
            Reason = reason?.Trim() ?? string.Empty
        });
    }
}