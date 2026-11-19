using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

using ScoutAgent.Api.Agents;

namespace ScoutAgent.Api.SubAgents;

// 019: katalog uzmanı — KENDİ LLM'i + model. İKİ tool'unu açıkça bilir: search_books (LİSTE) +
// get_book (TEKİL); her birinin girdisi elle, çıktısı bilinen tip (sniff/reflection yok).
public sealed class CatalogSubAgent(
    McpToolSource mcp, ChatClientFactory clients, ScoutOptions options)
    : ISubAgent, ISingletonDependency
{
    public string ToolName => "catalog_agent";

    private const string Description =
        "Kitap arama ve kitap künyesi/detayı: kitap adı, yazar, konu, fiyat/sayfa/yayınevi/cilt süzme, " +
        "sıralama, satışta/indirimli, 'devamını getir' sayfalama. Kitap keşfi hep buraya.";

    private const string Instructions =
        "Sen katalog uzmanısın. Kitap adı/yazar/konu geçince SORMADAN `search_books` çağır (sonuç boşsa " +
        "dürüstçe 'bulunamadı'). Belirli kitap detayı → `get_book`. 'devamını getir/sonraki' = SON aramanın " +
        "page+1'i. Sonuçlar kullanıcıya KART olarak gösteriliyor — tek tek listeleme, 1-2 cümle giriş yaz. " +
        "Fiyat/kapak/künye ASLA uydurma (yalnız araç dönüşü). İç GUID gösterme.";

    public AITool AsRouterTool(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (string request, CancellationToken ct) =>
            {
                var agent = new ChatClientAgent(
                    clients.ForModel(ScoutModelResolver.Resolve(options, "catalog")),
                    instructions: Instructions, name: ToolName,
                    tools: [SearchBooks(collector), GetBook(collector)]);
                var response = await agent.RunAsync(request, cancellationToken: ct);
                return response.Text ?? string.Empty;
            },
            name: ToolName, description: Description);

    // search_books — girdi elle (13 alan), çıktı SearchBooksResult (LİSTE).
    private AITool SearchBooks(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (string? query, int page, int pageSize, string? publisher, string? excludePublisher,
                   string? binding, decimal? minPrice, decimal? maxPrice, string? sort, bool onlySalable,
                   bool onlyDiscounted, int? minPageCount, int? maxPageCount, CancellationToken ct) =>
            {
                var client = await mcp.GetClientAsync("catalog", ct);
                var result = await client.CallToolAsync("search_books", new Dictionary<string, object?>
                {
                    ["query"] = query, ["page"] = page, ["pageSize"] = pageSize, ["publisher"] = publisher,
                    ["excludePublisher"] = excludePublisher, ["binding"] = binding, ["minPrice"] = minPrice,
                    ["maxPrice"] = maxPrice, ["sort"] = sort, ["onlySalable"] = onlySalable,
                    ["onlyDiscounted"] = onlyDiscounted, ["minPageCount"] = minPageCount,
                    ["maxPageCount"] = maxPageCount,
                }, cancellationToken: ct);
                var typed = result.StructuredContent is { } sc
                    ? sc.Deserialize<SearchBooksResult>(McpToolSource.JsonOptions) : null;
                collector.Add("search_books", typed);
                return typed;
            },
            name: "search_books",
            description: "Kitap adı/yazar + filtre/sıralama ile kitap arar; sayfalı liste döner. " +
                "sort: price_asc/price_desc/newest/popular. binding: karton/ciltli. Sayfalama: page/pageSize.");

    // get_book — girdi elle (isbn|id), çıktı GetBookResult (TEKİL; bulunamazsa null).
    private AITool GetBook(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (string? isbn, string? id, CancellationToken ct) =>
            {
                var client = await mcp.GetClientAsync("catalog", ct);
                var result = await client.CallToolAsync("get_book", new Dictionary<string, object?>
                {
                    ["isbn"] = isbn, ["id"] = id,
                }, cancellationToken: ct);
                var typed = result.StructuredContent is { } sc
                    ? sc.Deserialize<GetBookResult>(McpToolSource.JsonOptions) : null;
                collector.Add("get_book", typed);
                return typed;
            },
            name: "get_book",
            description: "Tek bir kitabın künyesini/detayını ISBN veya id ile getirir.");
}
