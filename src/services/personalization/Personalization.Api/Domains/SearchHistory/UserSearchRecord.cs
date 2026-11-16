namespace Personalization.Api.Domains.SearchHistory;

/// <summary>
/// Kullanıcı arama kaydı — Personalization BC'nin ham sinyal belgesi (012). Login bir kullanıcının
/// TEK aramasının değişmez izi: ne aradı (Query), hangi mod (Mode), kaç sonuç (ResultCount), ne zaman
/// (SearchedAt). Append-only — mutasyon/silme YOK; her arama ayrı satır (tekilleştirme yok, tekrar da
/// sinyaldir). Zevk profili/ONNX sonraki dilimlerde bundan türetilir. AggregateRoot DEĞİL (log satırı);
/// Marten belgesi (parametresiz private ctor + private set — Newtonsoft deserialize güvencesi).
/// </summary>
public class UserSearchRecord
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Query { get; private set; } = default!;
    public string Mode { get; private set; } = default!;
    public int ResultCount { get; private set; }
    public DateTimeOffset SearchedAt { get; private set; }

    private UserSearchRecord()
    {
    }

    /// <summary>Login aramasından kayıt üretir. Guard'lar: UserId boş olamaz (login şart), Query
    /// boş/whitespace olamaz, ResultCount negatif olamaz (0 geçerli — sonuçsuz arama kaydedilir).</summary>
    public static ResultDomain<UserSearchRecord> Create(
        Guid userId, string query, string mode, int resultCount, DateTimeOffset searchedAt)
    {
        if (userId == Guid.Empty)
            return ResultDomain<UserSearchRecord>.Error(new MessageItem
            { Property = nameof(UserId), Code = PersonalizationResourceConstants.SEARCH_USER_REQUIRED });
        if (string.IsNullOrWhiteSpace(query))
            return ResultDomain<UserSearchRecord>.Error(new MessageItem
            { Property = nameof(Query), Code = PersonalizationResourceConstants.SEARCH_QUERY_EMPTY });
        if (resultCount < 0)
            return ResultDomain<UserSearchRecord>.Error(new MessageItem
            { Property = nameof(ResultCount), Code = PersonalizationResourceConstants.SEARCH_RESULT_COUNT_INVALID });

        return ResultDomain<UserSearchRecord>.Ok(new UserSearchRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Query = query.Trim(),
            Mode = string.IsNullOrWhiteSpace(mode) ? "keyword" : mode.Trim(),
            ResultCount = resultCount,
            SearchedAt = searchedAt,
        });
    }
}
