using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

using ScoutAgent.Api.Agents;

namespace ScoutAgent.Api.SubAgents;

// 019: sepet uzmanı (login) — KENDİ LLM'i + model. Tek tool: get_basket (parametresiz, TEKİL aggregate).
public sealed class BasketSubAgent(
    McpToolSource mcp, ChatClientFactory clients, ScoutOptions options)
    : ISubAgent, ISingletonDependency
{
    public string ToolName => "basket_agent";

    private const string Description = "Giriş yapmış kullanıcının sepeti (içindeki kitaplar, tutar).";

    private const string Instructions =
        "Sen sepet uzmanısın. Kullanıcı sepetini sorarsa `get_basket` çağır. Araç başarısızsa giriş " +
        "gerektirir — nazikçe giriş iste. Sepete EKLEME yapamazsın (yalnız okuma). Uydurma yok. İç GUID gösterme.";

    public AITool AsRouterTool(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (string request, CancellationToken ct) =>
            {
                var agent = new ChatClientAgent(
                    clients.ForModel(ScoutModelResolver.Resolve(options, "basket")),
                    instructions: Instructions, name: ToolName, tools: [GetBasket(collector)]);
                var response = await agent.RunAsync(request, cancellationToken: ct);
                return response.Text ?? string.Empty;
            },
            name: ToolName, description: Description);

    private AITool GetBasket(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (CancellationToken ct) =>
            {
                var client = await mcp.GetClientAsync("basket", ct);
                var result = await client.CallToolAsync("get_basket", cancellationToken: ct);
                var typed = result.StructuredContent is { } sc
                    ? sc.Deserialize<GetBasketResult>(McpToolSource.JsonOptions) : null;
                collector.Add("get_basket", typed);
                return typed;
            },
            name: "get_basket",
            description: "Giriş yapmış kullanıcının sepetini (kalemler + tutar) getirir.");
}
