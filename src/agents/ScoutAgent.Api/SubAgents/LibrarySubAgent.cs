using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

using ScoutAgent.Api.Agents;

namespace ScoutAgent.Api.SubAgents;

// 019: kitaplık uzmanı (login) — KENDİ LLM'i + model. Tek tool: get_my_library (parametresiz, LİSTE).
public sealed class LibrarySubAgent(
    McpToolSource mcp, ChatClientFactory clients, ScoutOptions options)
    : ISubAgent, ISingletonDependency
{
    public string ToolName => "library_agent";

    private const string Description = "Giriş yapmış kullanıcının kitaplığı (satın aldığı kitaplar).";

    private const string Instructions =
        "Sen kitaplık uzmanısın. Kullanıcı kitaplığını sorarsa `get_my_library` çağır. Araç başarısızsa " +
        "giriş gerektirir — nazikçe giriş iste. Yalnız okuma. İç GUID gösterme.";

    public AITool AsRouterTool(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (string request, CancellationToken ct) =>
            {
                var agent = new ChatClientAgent(
                    clients.ForModel(ScoutModelResolver.Resolve(options, "library")),
                    instructions: Instructions, name: ToolName, tools: [GetMyLibrary(collector)]);
                var response = await agent.RunAsync(request, cancellationToken: ct);
                return response.Text ?? string.Empty;
            },
            name: ToolName, description: Description);

    private AITool GetMyLibrary(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (CancellationToken ct) =>
            {
                var client = await mcp.GetClientAsync("library", ct);
                var result = await client.CallToolAsync("get_my_library", cancellationToken: ct);
                var typed = result.StructuredContent is { } sc
                    ? sc.Deserialize<GetMyLibraryResult>(McpToolSource.JsonOptions) : null;
                collector.Add("get_my_library", typed);
                return typed;
            },
            name: "get_my_library",
            description: "Giriş yapmış kullanıcının kitaplığındaki ürünleri getirir.");
}
