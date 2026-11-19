using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

using ScoutAgent.Api.Agents;

namespace ScoutAgent.Api.SubAgents;

// 019: sipariş uzmanı (login) — KENDİ LLM'i + model. Tek tool: get_my_orders (LİSTE; status süzme).
public sealed class OrderSubAgent(
    McpToolSource mcp, ChatClientFactory clients, ScoutOptions options)
    : ISubAgent, ISingletonDependency
{
    public string ToolName => "order_agent";

    private const string Description = "Giriş yapmış kullanıcının siparişleri ve durumları.";

    private const string Instructions =
        "Sen sipariş uzmanısın. Kullanıcı siparişlerini sorarsa `get_my_orders` çağır (duruma göre " +
        "süzebilir: status). Araç başarısızsa giriş gerektirir — nazikçe giriş iste. Yalnız okuma. İç GUID gösterme.";

    public AITool AsRouterTool(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (string request, CancellationToken ct) =>
            {
                var agent = new ChatClientAgent(
                    clients.ForModel(ScoutModelResolver.Resolve(options, "order")),
                    instructions: Instructions, name: ToolName, tools: [GetMyOrders(collector)]);
                var response = await agent.RunAsync(request, cancellationToken: ct);
                return response.Text ?? string.Empty;
            },
            name: ToolName, description: Description);

    private AITool GetMyOrders(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (string? status, CancellationToken ct) =>
            {
                var client = await mcp.GetClientAsync("order", ct);
                var result = await client.CallToolAsync("get_my_orders", new Dictionary<string, object?>
                {
                    ["status"] = status,
                }, cancellationToken: ct);
                var typed = McpToolSource.DeserializeResult<GetMyOrdersResult>(result);
                collector.Add("get_my_orders", typed);
                return typed;
            },
            name: "get_my_orders",
            description: "Giriş yapmış kullanıcının siparişlerini getirir (opsiyonel status süzmesi).");
}
