using Marten;

namespace Library.Api.Domains.UserBookView.Features.Queries;

// Son Gezdiğim Kitaplar (2026-11-26): kullanıcının en yeni 20 gezme kaydı (en yeni önce). userId
// ZORUNLU (endpoint token'dan verir) — yalnız sahibin kaydı. Dedup zaten yazımda (deterministik Id);
// burada sadece sıralama + cap. Boş geçmiş hata değil → Ok(boş liste).
public static class GetRecentlyViewed
{
    public const int MaxItems = 20;

    public record GetRecentlyViewedQuery(Guid UserId);

    public record Item(Guid ProductId, string Title, string Author, string? CoverUrl, string Isbn, DateTimeOffset ViewedAt);

    public class RecentlyViewedResponse
    {
        public List<Item> Data { get; set; } = new();
    }

    public class Handler
    {
        public async Task<FeatureObjectResultModel<RecentlyViewedResponse>> Handle(
            GetRecentlyViewedQuery query, IQuerySession session, CancellationToken ct)
        {
            var views = await session.Query<UserBookView>()
                .Where(x => x.UserId == query.UserId)
                .OrderByDescending(x => x.ViewedAt)
                .Take(MaxItems)
                .ToListAsync(ct);

            var items = views
                .Select(v => new Item(v.ProductId, v.Title, v.Author, v.CoverUrl, v.Isbn, v.ViewedAt))
                .ToList();

            return FeatureObjectResultModel<RecentlyViewedResponse>.Ok(new RecentlyViewedResponse { Data = items });
        }
    }
}
