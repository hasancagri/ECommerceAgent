using System.ClientModel;
using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using OpenAI;

namespace ScoutAgent.Api.Agents;

// 019: model-keyed IChatClient üretir — router + her sub-agent kendi modeliyle çalışsın (FR-004).
// UseFunctionInvocation ŞART: modelin tool_call'ını OTOMATİK yürütür (MCP tool'u çağır, sonucu modele
// geri besle). Client'lar model adına göre cache'lenir (aynı model = tek client). Singleton (Scrutor).
public sealed class ChatClientFactory(ScoutOptions options) : ISingletonDependency
{
    private readonly ConcurrentDictionary<string, IChatClient> _clients = new();

    // modelName boş → ScoutOptions.Model varsayılanı. Çağıranlar model çözümlemesini
    // SubAgentDefinitions.ResolveModel/ResolveRouterModel ile yapar.
    public IChatClient ForModel(string? modelName)
    {
        var model = string.IsNullOrWhiteSpace(modelName) ? options.Model : modelName;
        return _clients.GetOrAdd(model, m =>
        {
            IChatClient client = new OpenAIClient(new ApiKeyCredential(options.ApiKey))
                .GetChatClient(m)
                .AsIChatClient();
            return client.AsBuilder().UseFunctionInvocation().Build();
        });
    }
}
