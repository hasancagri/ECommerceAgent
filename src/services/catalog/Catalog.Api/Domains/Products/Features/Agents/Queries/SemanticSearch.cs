using System.ComponentModel;
using Catalog.Api.Search;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace Catalog.Api.Domains.Products.Features.Agents.Queries;

// 026 Dilim-2 US1: "şunu betimleyen kitap" — anlam-temelli arama (query-only MCP tool; yalnız ScoutAgent).
// Betimleyici/kavramsal sorgu embed edilir → product_embedding kNN (kosinüs) + eşik → künye. BM25 KARIŞMAZ
// (rol-ayrımı; lexical search_books ayrı). SQL yürütümü SemanticSearchQueries'te. Yapısal filtre US3'te gelir.
// Agent slice — [McpServerToolType] aynı dosyada (conventions VSA).
public static class SemanticSearch
{
    public record Query(string Text, int Count, SemanticFilter Filter);

    public record BookItem(Guid Id, string Title, string Authors, decimal? Price, bool IsSalable,
        bool InStock, string? CoverUrl, string? Publisher, string? Isbn, string Genre, IReadOnlyList<string> Moods);

    public class Response
    {
        public IReadOnlyList<BookItem> Items { get; set; } = [];
        public long Total { get; set; }
    }

    public class SemanticSearchHandler(SemanticSearchQueries queries, IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Query query, CancellationToken ct)
        {
            var matches = await queries.SearchAsync(query.Text, query.Count, query.Filter, ct);
            if (matches.Count == 0)
                return FeatureObjectResultModel<Response>.Ok(new Response());

            // Yazar + yayınevi adları — tek sorguda (Id → ad; SearchBooks/SimilarBooks deseni).
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
        [McpServerTool(Name = "semantic_search")]
        [Description("Kitabın adını/yazarını bilmeyen, NE İSTEDİĞİNİ BETİMLEYEN sorgular için anlam-temelli " +
                     "arama ('umut veren hafif bir yaz romanı', 'kayıp üstüne sakin bir kitap'). Sorgunun " +
                     "ANLAMINA yakın kitaplar döner (kelimeler kitapta geçmese de). Başlık/yazar/ISBN TAM " +
                     "araması için search_books kullan. Anonim.")]
        public static async Task<Response> SemanticSearch(
            IMessageBus bus,
            ICurrentUser currentUser,
            IHttpContextAccessor http,
            [Description("Betimleyici/kavramsal sorgu metni")] string query,
            [Description("Kaç sonuç (varsayılan 10, en çok 20)")] int count = 10,
            [Description("Yalnız stoktakiler")] bool onlyInStock = false,
            [Description("Yalnız indirimliler")] bool onlyDiscounted = false,
            [Description("En düşük fiyat (TL)")] decimal? minPrice = null,
            [Description("En yüksek fiyat (TL)")] decimal? maxPrice = null,
            [Description("En az sayfa")] int? minPageCount = null,
            [Description("En çok sayfa")] int? maxPageCount = null,
            CancellationToken cancellationToken = default)
        {
            var filter = new SemanticFilter(onlyInStock, onlyDiscounted, minPrice, maxPrice, minPageCount, maxPageCount);
            var result = await bus.InvokeAsync<FeatureObjectResultModel<Response>>(
                new Query(query, count, filter), cancellationToken);

            // 026 (FR-007): login + dolu-sorguda UserSearched (mode="semantic") yayınla (search_books deseni).
            // Ayrık + hata-yutar — arama sonucunu ETKİLEMEZ. Anonim (Id boş)/boş sorgu → yayınlanmaz.
            currentUser.Load(http.HttpContext!.User);
            if (currentUser.Id != Guid.Empty && !string.IsNullOrWhiteSpace(query))
            {
                try
                {
                    await bus.PublishAsync(new IntegrationEvents.UserSearched(
                        currentUser.Id, query.Trim(), "semantic", (int)(result.Data?.Total ?? 0), DateTimeOffset.UtcNow));
                }
                catch { /* yayın arama sonucunu etkilemez */ }
            }

            return result.Data ?? new Response();
        }
    }
}
