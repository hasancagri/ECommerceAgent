using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace Catalog.Api.Domains.Products.Features.Agents.Queries;

// 007 US1: anonim kitap arama (ad + yazar ILIKE). Agent query slice — MCP tool sarmalayıcısı aynı
// dosyada (conventions VSA). İnce: tool → IMessageBus → handler → IQuerySession. Yazma yok, anonim.
public static class SearchBooks
{
    public record Query(string Search, int Page, int PageSize, string? Publisher = null, string? ExcludePublisher = null,
        int? Binding = null, decimal? MinPrice = null, decimal? MaxPrice = null, string? Sort = null,
        bool OnlySalable = false, bool OnlyDiscounted = false,
        int? MinPageCount = null, int? MaxPageCount = null);

    public record BookItem(Guid Id, string Title, string Authors, decimal? Price, bool IsSalable, string? CoverUrl, string? Publisher);

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

            // Sıralı birleştirme: YAZAR eşleşmeleri ÖNCE, sonra başlık eşleşmeleri; her grup kendi içinde
            // PurchaseCount'a göre. Çift kayıt (hem yazar hem başlık) yazar grubunda bir kez kalır.
            var ordered = new List<Guid>();
            var seen = new HashSet<Guid>();

            // AuthorIds Newtonsoft $type/$values sarmalayıcısıyla saklanıyor ({"$type":...,"$values":[...]})
            // → düz-dizi containment (p.AuthorIds.Contains(id)) jsonb'de EŞLEŞMEZ (yazar araması boş dönerdi).
            // Ham jsonb yoluyla $values dizisine iniyoruz: data->'AuthorIds'->'$values' @> ["id"].
            foreach (var authorId in authorIds)
            {
                var needle = $"[\"{authorId}\"]";
                var authorProducts = await session.QueryAsync<Product>(
                    "where d.data -> 'AuthorIds' -> '$values' @> ?::jsonb " +
                    "order by (d.data ->> 'PurchaseCount')::int desc nulls last", ct, needle);
                foreach (var p in authorProducts)
                    if (seen.Add(p.Id))
                        ordered.Add(p.Id);
            }

            // Başlık (ad ILIKE) eşleşmeleri — yazar kümesinde olmayanlar sona eklenir.
            var titleIds = await session.Query<Product>()
                .Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.PurchaseCount)
                .Select(p => p.Id)
                .Take(500)
                .ToListAsync(ct);
            foreach (var id in titleIds)
                if (seen.Add(id))
                    ordered.Add(id);

            // Süzgeç + sıralama — sayfalamadan ÖNCE uygulanır ki total süzülmüş sayıyı yansıtsın.
            // Aday ürünlerin alanları TEK sorguda yüklenir (PublisherId yayınevi süzgeci için; Binding/Price/
            // PublishedOn/PurchaseCount cilt-fiyat süzgeci ve sıralama için). Yalnız bir süzgeç/sıralama aktifse.
            var filterActive = !string.IsNullOrWhiteSpace(query.Publisher)
                || !string.IsNullOrWhiteSpace(query.ExcludePublisher)
                || query.Binding.HasValue || query.MinPrice.HasValue || query.MaxPrice.HasValue
                || !string.IsNullOrWhiteSpace(query.Sort) || query.OnlySalable || query.OnlyDiscounted
                || query.MinPageCount.HasValue || query.MaxPageCount.HasValue;
            if (ordered.Count > 0 && filterActive)
            {
                var orderedArr = ordered.ToArray();
                var candById = (await session.Query<Product>()
                        .Where(p => p.Id.IsOneOf(orderedArr))
                        .Select(p => new { p.Id, p.PublisherId, p.Binding, Price = p.Price != null ? (decimal?)p.Price.Amount : null, p.PublishedOn, p.PurchaseCount, p.DiscountRate, p.PageCount })
                        .ToListAsync(ct))
                    .ToDictionary(x => x.Id);

                async Task<HashSet<Guid>> MatchPublisherIds(string name) =>
                    (await session.Query<Publisher>()
                        .Where(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                        .Select(p => p.Id)
                        .ToListAsync(ct))
                    .ToHashSet();

                if (!string.IsNullOrWhiteSpace(query.Publisher))
                {
                    var include = await MatchPublisherIds(query.Publisher.Trim());
                    // Eşleşen yayınevi yoksa sonuç boş (dahil süzgeci hiçbir ürünü tutmaz).
                    ordered = ordered
                        .Where(id => candById.TryGetValue(id, out var c) && include.Contains(c.PublisherId))
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(query.ExcludePublisher))
                {
                    var exclude = await MatchPublisherIds(query.ExcludePublisher.Trim());
                    ordered = ordered
                        .Where(id => !(candById.TryGetValue(id, out var c) && exclude.Contains(c.PublisherId)))
                        .ToList();
                }

                // Cilt tipi süzgeci — enum int karşılığıyla eşleştir.
                if (query.Binding is { } bindingValue)
                {
                    var binding = (BindingType)bindingValue;
                    ordered = ordered
                        .Where(id => candById.TryGetValue(id, out var c) && c.Binding == binding)
                        .ToList();
                }

                // Fiyat aralığı süzgeci — süzgeç aktifken fiyatsız (null) ürün ELENİR.
                if (query.MinPrice is { } minPrice)
                    ordered = ordered
                        .Where(id => candById.TryGetValue(id, out var c) && c.Price.HasValue && c.Price.Value >= minPrice)
                        .ToList();
                if (query.MaxPrice is { } maxPrice)
                    ordered = ordered
                        .Where(id => candById.TryGetValue(id, out var c) && c.Price.HasValue && c.Price.Value <= maxPrice)
                        .ToList();

                // Sayfa sayısı aralığı — süzgeç aktifken sayfası bilinmeyen (null) ürün ELENİR.
                if (query.MinPageCount is { } minPages)
                    ordered = ordered
                        .Where(id => candById.TryGetValue(id, out var c) && c.PageCount.HasValue && c.PageCount.Value >= minPages)
                        .ToList();
                if (query.MaxPageCount is { } maxPages)
                    ordered = ordered
                        .Where(id => candById.TryGetValue(id, out var c) && c.PageCount.HasValue && c.PageCount.Value <= maxPages)
                        .ToList();

                // Satışta-olanlar süzgeci — IsSalable hesaplı (fiyat>0 ∧ stok>0 ∧ ≥1 yazar), LINQ'e
                // çevrilmez (AuthorIds jsonb) → aday tam-ürünleri yükleyip bellekte süz. Yalnız aktifken.
                if (query.OnlySalable)
                {
                    var salableIds = (await session.Query<Product>()
                            .Where(p => p.Id.IsOneOf(orderedArr)).ToListAsync(ct))
                        .Where(p => p.IsSalable).Select(p => p.Id).ToHashSet();
                    ordered = ordered.Where(salableIds.Contains).ToList();
                }

                // İndirimdeki kitaplar — DiscountRate dolu ve > 0 (null = indirim yok).
                if (query.OnlyDiscounted)
                    ordered = ordered
                        .Where(id => candById.TryGetValue(id, out var c) && c.DiscountRate is > 0)
                        .ToList();

                // Sıralama — verilmişse yazar-önce sırasını EZER; boş/bilinmeyen değer sırayı korur.
                // null fiyat/tarih en sona (OrderBy ... ThenBy null-son deseni).
                decimal? PriceOf(Guid id) => candById.TryGetValue(id, out var c) ? c.Price : null;
                DateOnly? PublishedOf(Guid id) => candById.TryGetValue(id, out var c) ? c.PublishedOn : null;
                int PurchaseOf(Guid id) => candById.TryGetValue(id, out var c) ? c.PurchaseCount : 0;
                ordered = (query.Sort?.Trim().ToLowerInvariant()) switch
                {
                    "price_asc" => ordered.OrderBy(id => PriceOf(id).HasValue ? 0 : 1).ThenBy(id => PriceOf(id)).ToList(),
                    "price_desc" => ordered.OrderBy(id => PriceOf(id).HasValue ? 0 : 1).ThenByDescending(id => PriceOf(id)).ToList(),
                    "newest" => ordered.OrderBy(id => PublishedOf(id).HasValue ? 0 : 1).ThenByDescending(id => PublishedOf(id)).ToList(),
                    "popular" => ordered.OrderByDescending(id => PurchaseOf(id)).ToList(),
                    _ => ordered,
                };
            }

            var total = ordered.Count;
            var pageIds = ordered.Skip((page - 1) * size).Take(size).ToArray();
            // IsOneOf sırayı garantilemez → yüklenen ürünleri pageIds sırasına geri diz.
            var byId = pageIds.Length == 0
                ? new Dictionary<Guid, Product>()
                : (await session.Query<Product>().Where(p => p.Id.IsOneOf(pageIds)).ToListAsync(ct))
                    .ToDictionary(p => p.Id);
            var pageItems = pageIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();

            // Sayfadaki ürünlerin yazar adlarını tek sorguda çöz (Id → ad).
            var neededAuthorIds = pageItems.SelectMany(p => p.AuthorIds).Distinct().ToList();
            var authorNameById = neededAuthorIds.Count > 0
                ? (await session.Query<Author>().Where(a => neededAuthorIds.Contains(a.Id)).ToListAsync(ct))
                    .ToDictionary(a => a.Id, a => a.Name)
                : new Dictionary<Guid, string>();

            // Sayfadaki ürünlerin yayınevi adlarını tek sorguda çöz (Id → ad); yazar adı çözümüyle aynı desen.
            var neededPublisherIds = pageItems.Select(p => p.PublisherId).Where(id => id != Guid.Empty).Distinct().ToList();
            var publisherNameById = neededPublisherIds.Count > 0
                ? (await session.Query<Publisher>().Where(p => neededPublisherIds.Contains(p.Id)).ToListAsync(ct))
                    .ToDictionary(p => p.Id, p => p.Name)
                : new Dictionary<Guid, string>();

            var items = pageItems.Select(p => new BookItem(
                p.Id,
                p.Name,
                string.Join(", ", p.AuthorIds
                    .Select(id => authorNameById.TryGetValue(id, out var n) ? n : null)
                    .Where(n => n is not null)),
                p.Price?.Amount,
                p.IsSalable,
                p.ImageUrl,
                publisherNameById.GetValueOrDefault(p.PublisherId))).ToList();

            return FeatureObjectResultModel<Response>.Ok(new Response { Items = items, Total = total, Page = page });
        }
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
                    onlySalable, onlyDiscounted, minPageCount, maxPageCount), cancellationToken);

            // 014 US4: login + dolu-sorguda UserSearched yayınla (fanout → Personalization 013). Arama
            // sonucunu ETKİLEMEZ — ayrık + hata-yutar. Anonim (Id boş) ya da boş sorgu → yayınlanmaz.
            // (012 REST publish'inin yerini aldı — tek yayın kaynağı artık bu tool; F1 kararı.)
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
