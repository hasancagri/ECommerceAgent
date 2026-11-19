using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

using ScoutAgent.Api.Agents;

namespace ScoutAgent.Api.SubAgents;

// 019: adres uzmanı (login) — KENDİ LLM'i + model. Tek tool: get_my_addresses (parametresiz, LİSTE).
public sealed class CustomerSubAgent(
    McpToolSource mcp, ChatClientFactory clients, ScoutOptions options)
    : ISubAgent, ISingletonDependency
{
    public string ToolName => "customer_agent";

    private const string Description = "Giriş yapmış kullanıcının adres defteri.";

    private const string Instructions =
        "Sen adres uzmanısın. Kullanıcı adreslerini sorarsa `get_my_addresses` çağır. Araç başarısızsa " +
        "giriş gerektirir — nazikçe giriş iste. Adres ekleme/değiştirme yapamazsın (yalnız okuma). İç GUID gösterme.";

    public AITool AsRouterTool(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (string request, CancellationToken ct) =>
            {
                var agent = new ChatClientAgent(
                    clients.ForModel(ScoutModelResolver.Resolve(options, "customer")),
                    instructions: Instructions, name: ToolName, tools: [GetMyAddresses(collector)]);
                var response = await agent.RunAsync(request, cancellationToken: ct);
                return response.Text ?? string.Empty;
            },
            name: ToolName, description: Description);

    private AITool GetMyAddresses(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (CancellationToken ct) =>
            {
                var client = await mcp.GetClientAsync("customer", ct);
                var result = await client.CallToolAsync("get_my_addresses", cancellationToken: ct);
                var typed = result.StructuredContent is { } sc
                    ? sc.Deserialize<GetMyAddressesResult>(McpToolSource.JsonOptions) : null;
                collector.Add("get_my_addresses", typed);
                return typed;
            },
            name: "get_my_addresses",
            description: "Giriş yapmış kullanıcının adres defterini getirir.");
}
