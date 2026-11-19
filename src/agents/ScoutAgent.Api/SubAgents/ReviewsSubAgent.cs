using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

using ScoutAgent.Api.Agents;

namespace ScoutAgent.Api.SubAgents;

// 019: yorum uzmanı — KENDİ LLM'i + model. Tek tool: get_book_reviews (LİSTE + ortalama).
public sealed class ReviewsSubAgent(
    McpToolSource mcp, ChatClientFactory clients, ScoutOptions options)
    : ISubAgent, ISingletonDependency
{
    public string ToolName => "reviews_agent";

    private const string Description = "Bir kitabın okur yorumları ve puan özeti.";

    private const string Instructions =
        "Sen yorum uzmanısın. Kullanıcı bir kitabın yorumlarını/puanını sorarsa `get_book_reviews` çağır; " +
        "ortalama puan + yorumları KISA özetle. Uydurma yok (yalnız araç dönüşü). İç GUID gösterme.";

    public AITool AsRouterTool(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (string request, CancellationToken ct) =>
            {
                var agent = new ChatClientAgent(
                    clients.ForModel(ScoutModelResolver.Resolve(options, "reviews")),
                    instructions: Instructions, name: ToolName, tools: [GetBookReviews(collector)]);
                var response = await agent.RunAsync(request, cancellationToken: ct);
                return response.Text ?? string.Empty;
            },
            name: ToolName, description: Description);

    private AITool GetBookReviews(ToolResultCollector collector) =>
        AIFunctionFactory.Create(
            async (Guid productId, int page, int pageSize, CancellationToken ct) =>
            {
                var client = await mcp.GetClientAsync("reviews", ct);
                var result = await client.CallToolAsync("get_book_reviews", new Dictionary<string, object?>
                {
                    ["productId"] = productId, ["page"] = page, ["pageSize"] = pageSize,
                }, cancellationToken: ct);
                var typed = result.StructuredContent is { } sc
                    ? sc.Deserialize<GetBookReviewsResult>(McpToolSource.JsonOptions) : null;
                collector.Add("get_book_reviews", typed);
                return typed;
            },
            name: "get_book_reviews",
            description: "Bir kitabın (productId) yorumlarını + ortalama puanını sayfalı getirir.");
}
