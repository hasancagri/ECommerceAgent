using Marten;

namespace Personalization.Api.Domains.SearchHistory.Features.Queries;

// 013 US1: kullanıcının tekilleştirilmiş + sayfalı arama geçmişi (en yeni üstte). userId ZORUNLU
// (endpoint token'dan verir) — başka kullanıcının kaydı ASLA. Ham kayıtlar çekilir, saf projection
// tekilleştirir, sonra sayfalanır. Boş geçmiş hata değil (FR-009) → Ok(boş response).
public static class GetSearchHistory
{
    public record GetSearchHistoryQuery(Guid UserId, int Page, int PageSize);

    public class SearchHistoryResponse
    {
        public List<SearchHistoryItem> Data { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    // NormalizedQuery API dışına sızmaz (dedup/silme-iç detayı).
    public record SearchHistoryItem(string Query, string Mode, DateTimeOffset LastSearchedAt, int Count);

    public class Handler
    {
        public async Task<FeatureObjectResultModel<SearchHistoryResponse>> Handle(
            GetSearchHistoryQuery query, IQuerySession session, CancellationToken ct)
        {
            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

            var records = await session.Query<UserSearchRecord>()
                .Where(x => x.UserId == query.UserId)
                .ToListAsync(ct);

            var deduped = SearchHistoryProjection.Deduplicate(records);

            var items = deduped
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new SearchHistoryItem(e.Query, e.Mode, e.LastSearchedAt, e.Count))
                .ToList();

            return FeatureObjectResultModel<SearchHistoryResponse>.Ok(new SearchHistoryResponse
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = deduped.Count,
            });
        }
    }
}
