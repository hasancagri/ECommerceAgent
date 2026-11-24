using System.ComponentModel;
using Catalog.Api.Search;
using ModelContextProtocol.Server;

namespace Catalog.Api.Domains.Products.Features.Agents.Queries;

// 028 Dilim-4: kişisel öneri — verilen TASTE vektörüne `product_embedding` kNN (anlam) + satılabilir +
// WorkKey tekil + çeşitlilik. Taste BOŞ → küratörlü (popülerlik). ANONİM tool (taste'i ScoutAgent sağlar;
// kullanıcı-kimliği Personalization'da çözülür, burada yok). Vektör opak sayı dizisi (İlke VIII; LLM değil,
// ScoutAgent programatik set eder — feed deterministik). SQL yürütümü RecommendQueries'te.
public static class RecommendBooks
{
    public record Query(float[] Taste, int Count);

    public record BookItem(Guid Id, string Title, string Authors, decimal? Price, bool IsSalable,
        bool InStock, string? CoverUrl, string? Publisher, string? Isbn, string Genre, IReadOnlyList<string> Moods);

    public class Response
    {
        public IReadOnlyList<BookItem> Items { get; set; } = [];
        public long Total { get; set; }
    }

    public class RecommendBooksHandler(RecommendQueries queries, IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Query query, CancellationToken ct)
        {
            var matches = await queries.RecommendAsync(query.Taste ?? [], query.Count, ct);
            if (matches.Count == 0)
                return FeatureObjectResultModel<Response>.Ok(new Response());

            var authorIds = matches.SelectMany(p => p.AuthorIds).Distinct().ToList();
            var authorNameById = authorIds.Count > 0
                ? (await session.Query<Author>().Where(a => authorIds.Contains(a.Id)).ToListAsync(ct))
                    .ToDictionary(a => a.Id, a => a.Name)
                : new Dictionary<Guid, string>();
            var publisherIds = matches.Select(p => p.PublisherId).Where(id => id != Guid.Empty).Distinct().ToList();
            var publisherNameById = publisherIds.Count > 0
                ? (await session.Query<Publisher>().Where(p => publisherIds.Contains(p.Id)).ToListAsync(ct))
                    .ToDictionary(p => p.Id, p => p.Name)
                : new Dictionary<Guid, string>();

            var items = matches.Select(p => new BookItem(
                p.Id, p.Name,
                string.Join(", ", p.AuthorIds.Select(id => authorNameById.GetValueOrDefault(id)).Where(n => n is not null)),
                p.Price?.Amount, p.IsSalable, p.InStock, p.ImageUrl,
                publisherNameById.GetValueOrDefault(p.PublisherId), p.Gtin,
                p.Genre, p.Moods.ToList())).ToList();

            return FeatureObjectResultModel<Response>.Ok(new Response { Items = items, Total = items.Count });
        }
    }

    [McpServerToolType]
    public static class Tool
    {
        [McpServerTool(Name = "recommend_books")]
        [Description("Kişisel öneri: verilen zevk-vektörüne (taste) ANLAMCA en yakın kitaplar + çeşitlilik + " +
                     "satılabilir + eser başına bir. Taste BOŞ ise küratörlü (popülerlik) döner. Yalnız ScoutAgent " +
                     "feed'i çağırır (taste'i Personalization get_my_taste'ten alır). Anonim.")]
        public static async Task<Response> RecommendBooks(
            IMessageBus bus,
            [Description("Zevk-vektörü (float dizisi; boş → küratörlü)")] float[] taste,
            [Description("Kaç öneri (varsayılan 10, en çok 20)")] int count = 10,
            CancellationToken cancellationToken = default)
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<Response>>(
                new Query(taste ?? [], count), cancellationToken);
            return result.Data ?? new Response();
        }
    }
}
