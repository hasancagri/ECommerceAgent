using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

using ScoutAgent.Api.Agents;

namespace ScoutAgent.Api.SubAgents;

// 019: arama-geçmişi uzmanı — KENDİ LLM'i + model (US2). Tool'larını AÇIKÇA bilir; her tool'un girdisi
// elle deklare, çıktısı BİLİNEN tip (GetSearchHistoryResult) → structuredContent.Deserialize<T> (sniff/
// reflection YOK). Router bu sub-agent'ı lazy çağırır; sub-agent LLM'i kendi tool'undan seçer.
public sealed class PersonalizationSubAgent(
    McpToolSource mcp, ChatClientFactory clients, ScoutOptions options)
    : ISubAgent, ISingletonDependency
{
    public string ToolName => "personalization_agent";

    private const string Description = "Giriş yapmış kullanıcının arama geçmişi.";

    private const string Instructions =
        "Sen arama-geçmişi uzmanısın. Kullanıcı geçmişini sorarsa `get_search_history` çağır. Araç " +
        "başarısızsa giriş gerektirir — nazikçe giriş iste. Geçmiş SİLME isteğinde geçmiş EKRANINDAN " +
        "silebileceğini söyle (yalnız okuma). Sonuçlar kullanıcıya kart olarak gösteriliyor — kısa " +
        "giriş yaz, tek tek listeleme. İç GUID gösterme.";

    public AITool AsRouterTool(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (string request, CancellationToken ct) =>
            {
                var agent = new ChatClientAgent(
                    clients.ForModel(ScoutModelResolver.Resolve(options, "personalization")),
                    instructions: Instructions, name: ToolName, tools: [GetSearchHistory(collector)]);
                var response = await agent.RunAsync(request, cancellationToken: ct);
                return response.Text ?? string.Empty;
            },
            name: ToolName, description: Description);

    // get_search_history — girdi elle deklare (page/pageSize), çıktı BİLİNEN tip.
    private AITool GetSearchHistory(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (int page, int pageSize, CancellationToken ct) =>
            {
                var client = await mcp.GetClientAsync("personalization", ct);
                var result = await client.CallToolAsync(
                    "get_search_history",
                    new Dictionary<string, object?> { ["page"] = page, ["pageSize"] = pageSize },
                    cancellationToken: ct);
                // Sonuç BİLİNEN tipe (sniff/reflection yok). structuredContent yoksa TextContent JSON. Null ise boş.
                var typed = McpToolSource.DeserializeResult<GetSearchHistoryResult>(result);
                collector.Add("get_search_history", typed);
                return typed;
            },
            name: "get_search_history",
            description: "Kullanıcının arama geçmişini getirir (tekilleştirilmiş, sayfalı).");
}
