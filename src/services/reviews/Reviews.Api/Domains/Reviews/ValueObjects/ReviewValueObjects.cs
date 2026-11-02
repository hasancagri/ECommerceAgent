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