using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Catalog.Api.Domains.Products.Features.Agents.Queries;

// 007 US1: anonim kitap arama (ad + yazar ILIKE). Agent query slice — MCP tool sarmalayıcısı aynı
// dosyada (conventions VSA). İnce: tool → IMessageBus → handler → IQuerySession. Yazma yok, anonim.
public static class SearchBooks
{
    public record Query(string Search, int Page, int PageSize);

    public record BookItem(Guid Id, string Title, string Authors, decimal? Price, bool IsSalable, string? CoverUrl);

    public class Response
    {
        public IReadOnlyList<BookItem> Items { get; set; } = [];
        public long Total { get; set; }
        public int Page { get; set; }
    }

    public class SearchBooksHandler(IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Query query, CancellationToken ct)
        {
            var term = (query.Search ?? string.Empty).Trim();
            var page = query.Page < 1 ? 1 : query.Page;
            var size = query.PageSize is < 1 or > 20 ? 10 : query.PageSize;

            if (term.Length == 0)
                return FeatureObjectResultModel<Response>.Ok(new Response { Page = page });

            // Yazar adı eşleşmeleri → id'ler (ad VEYA yazar araması için).
            var authorIds = await session.Query<Author>()
                .Where(a => a.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Select(a => a.Id)
                .Take(50)
                .ToListAsync(ct);

            // Marten-güvenli: ad ILIKE (tek sorgu) + yazar başına jsonb Contains (tek-değer). "Collection
            // Any(external list Contains)" Marten'de çevrilemiyor (çalışma anı fırlatır) — onun yerine
            // eşleşen ürün Id kümesi bellekte birleştirilir, sayfa Id'lerle tek sorguda yüklenir.
            var matchIds = new HashSet<Guid>(
                await session.Query<Product>()
                    .Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Id)
                    .Take(500)
                    .ToListAsync(ct));

            // AuthorIds Newtonsoft $type/$values sarmalayıcısıyla saklanıyor ({"$type":...,"$values":[...]})
            // → düz-dizi containment (p.AuthorIds.Contains(id)) jsonb'de EŞLEŞMEZ (yazar araması boş dönerdi).
            // Ham jsonb yoluyla $values dizisine iniyoruz: data->'AuthorIds'->'$values' @> ["id"].
            foreach (var authorId in authorIds)
            {
                var needle = $"[\"{authorId}\"]";
                var authorProducts = await session.QueryAsync<Product>(
                    "where d.data -> 'AuthorIds' -> '$values' @> ?::jsonb", ct, needle);
                foreach (var p in authorProducts)
                    matchIds.Add(p.Id);
            }

            var total = matchIds.Count;
            var pageIds = matchIds.Skip((page - 1) * size).Take(size).ToArray();
            var pageItems = pageIds.Length == 0
                ? []
                : (await session.Query<Product>().Where(p => p.Id.IsOneOf(pageIds)).ToListAsync(ct))
                    .OrderByDescending(p => p.PurchaseCount)
                    .ToList();

            // Sayfadaki ürünlerin yazar adlarını tek sorguda çöz (Id → ad).
            var neededAuthorIds = pageItems.SelectMany(p => p.AuthorIds).Distinct().ToList();
            var authorNameById = neededAuthorIds.Count > 0
                ? (await session.Query<Author>().Where(a => neededAuthorIds.Contains(a.Id)).ToListAsync(ct))
                    .ToDictionary(a => a.Id, a => a.Name)
                : new Dictionary<Guid, string>();

            var items = pageItems.Select(p => new BookItem(
                p.Id,
                p.Name,
                string.Join(", ", p.AuthorIds
                    .Select(id => authorNameById.TryGetValue(id, out var n) ? n : null)
                    .Where(n => n is not null)),
                p.Price?.Amount,
                p.IsSalable,
                p.ImageUrl)).ToList();

            return FeatureObjectResultModel<Response>.Ok(new Response { Items = items, Total = total, Page = page });
        }
    }

    [McpServerToolType]
    public static class Tool
    {
        [McpServerTool(Name = "search_books")]
        [Description("Katalogda kitap adı veya yazar adıyla arama yapar; eşleşen kitapları " +
                     "(ad, yazar, fiyat, satılabilirlik, kapak) döner. Kişisel veri İÇERMEZ, anonim.")]
        public static async Task<Response> SearchBooks(
            IMessageBus bus,
            [Description("Aranacak kitap adı veya yazar adı (serbest metin)")] string query,
            [Description("Sayfa numarası (1'den başlar)")] int page = 1,
            [Description("Sayfa boyutu (en fazla 20)")] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<Response>>(
                new Query(query, page, pageSize), cancellationToken);
            return result.Data ?? new Response { Page = page };
        }
    }
}
