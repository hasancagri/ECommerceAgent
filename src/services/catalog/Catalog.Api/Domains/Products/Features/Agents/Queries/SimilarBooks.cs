using System.ComponentModel;
using Catalog.Api.Search;
using ModelContextProtocol.Server;

namespace Catalog.Api.Domains.Products.Features.Agents.Queries;

// 021 US3: "şuna benzer kitap" — query-only MCP tool (yalnız ScoutAgent). Hedef kitabın konu+ton
// örtüşmesi (BM25 IDF) + objektif boost (yazar/çevirmen/tür) + hard filtre (dil, seviye uyum grubu,
// kendi baskıları hariç) + WorkKey tekilleştirme (eser başına 1) + stok-önce sıralama. V1 kaba iskelet
// (gerçek ayrım V2 embedding, ayrı spec). SQL yürütümü SimilarBooksQueries'te; buradaki Overlap SAF
// (test-first). Agent slice — [McpServerToolType] aynı dosyada (conventions VSA).
public static class SimilarBooks
{
    public record Query(Guid ProductId, int Count);

    public record BookItem(Guid Id, string Title, string Authors, decimal? Price, bool IsSalable,
        bool InStock, string? CoverUrl, string? Publisher, string? Isbn, string Genre, IReadOnlyList<string> Moods);

    public class Response
    {
        public IReadOnlyList<BookItem> Items { get; set; } = [];
        public long Total { get; set; }
    }

    // SAF birim (test-first): hedefin konu+ton değerlerinden BM25 OR örtüşme sorgusu. Boş → null (fallback).
    public static class Overlap
    {
        public static string? BuildBm25Query(IEnumerable<string> themes, IEnumerable<string> moods)
        {
            var terms = new List<string>();
            foreach (var t in themes)
                if (!string.IsNullOrWhiteSpace(t)) terms.Add($"data.Themes:\"{Escape(t)}\"");
            foreach (var m in moods)
                if (!string.IsNullOrWhiteSpace(m)) terms.Add($"data.Moods:\"{Escape(m)}\"");
            return terms.Count == 0 ? null : string.Join(" OR ", terms);
        }

        // Savunma: sözlük değerleri tırnak taşımaz ama gömülü " kaçırılır (sorgu kırılmaz).
        private static string Escape(string value) => value.Trim().Replace("\"", "\\\"");
    }

    public class SimilarBooksHandler(SimilarBooksQueries queries, SimilarBooksOptions options, IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Query query, CancellationToken ct)
        {
            var target = await session.LoadAsync<Product>(query.ProductId, ct);
            if (target is null)
                return FeatureObjectResultModel<Response>.NotFound();

            var count = options.ClampCount(query.Count);
            var similar = await queries.FindSimilarAsync(target, count, ct);
            if (similar.Count == 0)
                return FeatureObjectResultModel<Response>.Ok(new Response());

            // Yazar + yayınevi adları — tek sorguda (Id → ad; SearchBooks deseni).
            var authorIds = similar.SelectMany(p => p.AuthorIds).Distinct().ToList();
            var authorNameById = authorIds.Count > 0
                ? (await session.Query<Author>().Where(a => authorIds.Contains(a.Id)).ToListAsync(ct))
                    .ToDictionary(a => a.Id, a => a.Name)
                : new Dictionary<Guid, string>();
            var publisherIds = similar.Select(p => p.PublisherId).Where(id => id != Guid.Empty).Distinct().ToList();
            var publisherNameById = publisherIds.Count > 0
                ? (await session.Query<Publisher>().Where(p => publisherIds.Contains(p.Id)).ToListAsync(ct))
                    .ToDictionary(p => p.Id, p => p.Name)
                : new Dictionary<Guid, string>();

            var items = similar.Select(p => new BookItem(
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
        [McpServerTool(Name = "similar_books")]
        [Description("Verilen bir kitaba (productId) BENZER kitapları döner: konu+ton örtüşmesi + " +
                     "aynı yazar/tür yakınlığı. Aynı eserin diğer baskıları GELMEZ, her eser bir kez, " +
                     "stokta olanlar önce. Önce search_books ile kitabı bulup productId'sini al. Anonim.")]
        public static async Task<Response> SimilarBooks(
            IMessageBus bus,
            [Description("Benzerleri istenen kitabın productId'si (GUID; search_books sonucundan)")] Guid productId,
            [Description("Kaç benzer kitap (varsayılan 5, en çok 20)")] int count = 5,
            CancellationToken cancellationToken = default)
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<Response>>(
                new Query(productId, count), cancellationToken);
            return result.Data ?? new Response();
        }
    }
}
