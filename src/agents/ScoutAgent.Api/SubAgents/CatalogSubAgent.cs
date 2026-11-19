using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

using ScoutAgent.Api.Agents;

namespace ScoutAgent.Api.SubAgents;

// 019/018: katalog uzmanı — KENDİ LLM'i + model. TEK tool: search_books (LİSTE); girdisi elle, çıktısı
// bilinen tip (sniff/reflection yok). get_book (tekil) 018'de SÖKÜLDÜ — tekil kitap da search_books
// (tam ad → en-iyi eşleşme kartı); derin künye React detay sayfasında (kart tıkla), sohbette değil.
public sealed class CatalogSubAgent(
    McpToolSource mcp, ChatClientFactory clients, ScoutOptions options)
    : ISubAgent, ISingletonDependency
{
    public string ToolName => "catalog_agent";

    private const string Description =
        "Kitap arama ve kitap künyesi/detayı: kitap adı, yazar, konu, fiyat/sayfa/yayınevi/cilt süzme, " +
        "sıralama, satışta/indirimli, 'devamını getir' sayfalama. Kitap keşfi hep buraya.";

    private const string Instructions =
        "Sen katalog uzmanısın. HER kitap-keşfi isteğinde SORMADAN `search_books` çağır — ad/yazar OLMASA " +
        "da (gezinme/öneri/popülerlik de arama): mesaj kitapla ilgiliyse tool'u MUTLAKA çağır, boş-elle " +
        "'bulamadım' DEME. Niyeti parametreye çevir: ad/yazar/konu → `query`; yoksa `query` BOŞ bırak + " +
        "filtre/sort kullan. 'en çok satan/popüler/gözde' → sort=popular; 'yeni/yeni çıkan' → sort=newest; " +
        "'ucuz/ucuzdan' → price_asc, 'pahalı' → price_desc; 'indirimli' → onlyDiscounted; 'satışta/stokta' → " +
        "onlySalable. `category` SADECE kullanıcı gerçek bir kategori adı söylerse (ör. 'Roman','Felsefe') — " +
        "niyet cümlesini category'ye KOYMA. Belirli bir kitabın detayı → `search_books` tam adıyla (en-iyi " +
        "eşleşme kartı; derin künye kartın detay sayfasında). 'devamını getir/sonraki' = SON aramanın " +
        "page+1'i. Sonuç GERÇEKTEN boşsa dürüstçe 'bulunamadı'. Sonuçlar kullanıcıya KART olarak gösterilir " +
        "— tek tek listeleme, 1-2 cümle giriş yaz. Fiyat/kapak/künye ASLA uydurma. İç GUID gösterme.";

    public AITool AsRouterTool(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (string request, CancellationToken ct) =>
            {
                var agent = new ChatClientAgent(
                    clients.ForModel(ScoutModelResolver.Resolve(options, "catalog")),
                    instructions: Instructions, name: ToolName,
                    tools: [SearchBooks(collector)]);
                var response = await agent.RunAsync(request, cancellationToken: ct);
                return response.Text ?? string.Empty;
            },
            name: ToolName, description: Description);

    // search_books — girdi elle (13 alan), çıktı SearchBooksResult (LİSTE).
    private AITool SearchBooks(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (string? query, int page, int pageSize, string? publisher, string? excludePublisher,
                   string? binding, decimal? minPrice, decimal? maxPrice, string? sort, bool onlySalable,
                   bool onlyDiscounted, int? minPageCount, int? maxPageCount, string? category,
                   CancellationToken ct) =>
            {
                var client = await mcp.GetClientAsync("catalog", ct);
                var result = await client.CallToolAsync("search_books", new Dictionary<string, object?>
                {
                    ["query"] = query, ["page"] = page, ["pageSize"] = pageSize, ["publisher"] = publisher,
                    ["excludePublisher"] = excludePublisher, ["binding"] = binding, ["minPrice"] = minPrice,
                    ["maxPrice"] = maxPrice, ["sort"] = sort, ["onlySalable"] = onlySalable,
                    ["onlyDiscounted"] = onlyDiscounted, ["minPageCount"] = minPageCount,
                    ["maxPageCount"] = maxPageCount, ["category"] = category,
                }, cancellationToken: ct);
                var typed = McpToolSource.DeserializeResult<SearchBooksResult>(result);
                collector.Add("search_books", typed);
                return typed;
            },
            name: "search_books",
            description: "Kitap adı/yazar + filtre/sıralama ile kitap arar; sayfalı liste döner. " +
                "sort: price_asc/price_desc/newest/popular. binding: karton/ciltli. category: kategori adı " +
                "(ör. 'Roman'). Sayfalama: page/pageSize.");
}
