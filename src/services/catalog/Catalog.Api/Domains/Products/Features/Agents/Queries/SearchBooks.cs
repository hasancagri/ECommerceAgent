using System.ComponentModel;
using Catalog.Api.Search;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace Catalog.Api.Domains.Products.Features.Agents.Queries;

// 018 US1: anonim kitap arama — TEK birleşik yol. Naive bellekte-filtre (aday yükle + LINQ) SÖKÜLDÜ;
// tüm filtre/sort/sayfalama SearchRequest'e dolar, ProductSearchQueries.SearchAsync tek ParadeDB
// sorgusuyla çözer. Agent query slice — MCP tool sarmalayıcısı aynı dosyada (conventions VSA).
public static class SearchBooks
{
    public record Query(string? Search, int Page, int PageSize, string? Publisher = null, string? ExcludePublisher = null,
        int? Binding = null, decimal? MinPrice = null, decimal? MaxPrice = null, string? Sort = null,
        bool OnlySalable = false, bool OnlyDiscounted = false,
        int? MinPageCount = null, int? MaxPageCount = null, string? Category = null);

    public record BookItem(Guid Id, string Title, string Authors, decimal? Price, bool IsSalable, string? CoverUrl, string? Publisher, string? Isbn);

    public class Response
    {
        public IReadOnlyList<BookItem> Items { get; set; } = [];
        public long Total { get; set; }
        public int Page { get; set; }
    }

    public class SearchBooksHandler(ProductSearchQueries queries, IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Query query, CancellationToken ct)
        {
            var page = query.Page < 1 ? 1 : query.Page;
            var size = query.PageSize is < 1 or > 20 ? 10 : query.PageSize;

            // Kategori adı/GUID → tek-leaf CategoryId. Verilip çözülemezse sonuç boş (filtre hiçbir şey tutmaz).
            Guid? categoryId = null;
            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                categoryId = await ResolveCategoryIdAsync(query.Category.Trim(), ct);
                if (categoryId is null)
                    return FeatureObjectResultModel<Response>.Ok(new Response { Page = page });
            }

            var request = new SearchRequest(
                Text: query.Search,
                Publisher: query.Publisher,
                ExcludePublisher: query.ExcludePublisher,
                PriceMin: query.MinPrice,
                PriceMax: query.MaxPrice,
                PageMin: query.MinPageCount,
                PageMax: query.MaxPageCount,
                Binding: query.Binding is { } b ? (BindingType)b : null,
                CategoryId: categoryId,
                OnlySalable: query.OnlySalable,
                OnlyDiscounted: query.OnlyDiscounted,
                Sort: ParseSort(query.Sort),
                Page: page,
                PageSize: size);

            var result = await queries.SearchAsync(request, ct);

            // Sayfa ürünlerinin yazar + yayınevi adları — her biri tek sorguda (Id → ad).
            var authorIds = result.Items.SelectMany(p => p.AuthorIds).Distinct().ToList();
            var authorNameById = authorIds.Count > 0
                ? (await session.Query<Author>().Where(a => authorIds.Contains(a.Id)).ToListAsync(ct))
                    .ToDictionary(a => a.Id, a => a.Name)
                : new Dictionary<Guid, string>();
            var publisherIds = result.Items.Select(p => p.PublisherId).Where(id => id != Guid.Empty).Distinct().ToList();
            var publisherNameById = publisherIds.Count > 0
                ? (await session.Query<Publisher>().Where(p => publisherIds.Contains(p.Id)).ToListAsync(ct))
                    .ToDictionary(p => p.Id, p => p.Name)
                : new Dictionary<Guid, string>();

            var items = result.Items.Select(p => new BookItem(
                p.Id,
                p.Name,
                string.Join(", ", p.AuthorIds
                    .Select(id => authorNameById.GetValueOrDefault(id))
                    .Where(n => n is not null)),
                p.Price?.Amount,
                p.IsSalable,
                p.ImageUrl,
                publisherNameById.GetValueOrDefault(p.PublisherId),
                p.Gtin)).ToList();   // 015: ISBN → kart SEO-link'i /kitap/{slug}-{isbn} kurar

            return FeatureObjectResultModel<Response>.Ok(new Response { Items = items, Total = result.Total, Page = page });
        }

        // Kategori adı (Türkçe-fold) ya da GUID → tek-leaf CategoryId; eşleşmezse null.
        private async Task<Guid?> ResolveCategoryIdAsync(string category, CancellationToken ct)
        {
            if (Guid.TryParse(category, out var g)) return g;
            var normalized = NameNormalization.Normalize(category);
            var match = await session.Query<Category>()
                .Where(c => c.NormalizedName == normalized && c.ParentCategoryId != null)
                .FirstOrDefaultAsync(ct);
            return match?.Id;
        }

        // Serbest sort metni → SortOption (REST ucuyla aynı sözlük; bilinçli tekrar — agent slice izole).
        private static SortOption? ParseSort(string? sort) => sort?.Trim().ToLowerInvariant() switch
        {
            "price_asc" => SortOption.PriceAsc,
            "price_desc" => SortOption.PriceDesc,
            "newest" => SortOption.Newest,
            "popular" => SortOption.Popular,
            _ => null,
        };
    }

    [McpServerToolType]
    public static class Tool
    {
        [McpServerTool(Name = "search_books")]
        [Description("Katalogda kitap adı veya yazar adıyla arama yapar; eşleşen kitapları " +
                     "(ad, yazar, fiyat, satılabilirlik, kapak) döner. Anonim erişilebilir.")]
        public static async Task<Response> SearchBooks(
            IMessageBus bus, ICurrentUser currentUser, IHttpContextAccessor http,
            [Description("Aranacak kitap adı veya yazar adı (serbest metin)")] string? query = null,
            [Description("Sayfa numarası (1'den başlar)")] int page = 1,
            [Description("Sayfa boyutu (en fazla 20)")] int pageSize = 10,
            [Description("YALNIZ bu yayınevinden; kısmi ad eşleşir, ör. 'İş Kültür'")] string? publisher = null,
            [Description("Bu yayınevini HARİÇ tut")] string? excludePublisher = null,
            [Description("Cilt tipi süzme: 'karton' (Karton Kapak) ya da 'ciltli'")] string? binding = null,
            [Description("En düşük fiyat (TL)")] decimal? minPrice = null,
            [Description("En yüksek fiyat (TL)")] decimal? maxPrice = null,
            [Description("Sıralama: 'price_asc', 'price_desc', 'newest', 'popular'")] string? sort = null,
            [Description("true → yalnız satışa uygun kitaplar (fiyatı var, stokta, yazarlı)")] bool onlySalable = false,
            [Description("true → yalnız indirimdeki kitaplar")] bool onlyDiscounted = false,
            [Description("En az sayfa sayısı (kalın/uzun kitap)")] int? minPageCount = null,
            [Description("En fazla sayfa sayısı (az sayfa/kısa kitap)")] int? maxPageCount = null,
            [Description("Kategori filtresi — tek-leaf CategoryId ya da kategori adı (ör. 'Roman')")] string? category = null,
            CancellationToken cancellationToken = default)
        {
            // Cilt tipi serbest metni enum int'ine çevrilir (içerir, büyük/küçük harf duyarsız); eşleşmezse null.
            int? bindingValue = binding?.ToLowerInvariant() switch
            {
                { } b when b.Contains("karton") => (int)BindingType.KartonKapak,
                { } b when b.Contains("ciltli") => (int)BindingType.Ciltli,
                _ => null,
            };

            var result = await bus.InvokeAsync<FeatureObjectResultModel<Response>>(
                new Query(query, page, pageSize, publisher, excludePublisher, bindingValue, minPrice, maxPrice, sort,
                    onlySalable, onlyDiscounted, minPageCount, maxPageCount, category), cancellationToken);

            // 014 US4: login + dolu-sorguda UserSearched yayınla (fanout → Personalization 013). Arama
            // sonucunu ETKİLEMEZ — ayrık + hata-yutar. Anonim (Id boş) ya da boş sorgu → yayınlanmaz.
            currentUser.Load(http.HttpContext!.User);
            if (currentUser.Id != Guid.Empty && !string.IsNullOrWhiteSpace(query))
            {
                try
                {
                    await bus.PublishAsync(new IntegrationEvents.UserSearched(
                        currentUser.Id, query.Trim(), "keyword", (int)(result.Data?.Total ?? 0), DateTimeOffset.UtcNow));
                }
                catch { /* yayın arama sonucunu etkilemez */ }
            }

            return result.Data ?? new Response { Page = page };
        }
    }
}
