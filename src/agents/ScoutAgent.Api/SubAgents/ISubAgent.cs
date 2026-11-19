using Microsoft.Extensions.AI;

using ScoutAgent.Api.Agents;

namespace ScoutAgent.Api.SubAgents;

// 019: BC-başına uzman sub-agent. Her biri KENDİ sınıfı (teker teker yazılır, bilinçli tekrar — repo
// ethos'u). Router bunları `IEnumerable<ISubAgent>` ile enjekte alıp tool olarak sunar; LLM ihtiyaca
// göre bir veya birden çok sub-agent'ı devreye alır. AsRouterTool LAZY olmalı: yalnız çağrılınca ilgili
// MCP ucuna bağlanır (seçilmeyen uç hiç yoklanmaz — SC-001).
public interface ISubAgent
{
    // Router'a sunulan tool adı (LLM bununla seçer). Sub-agent'lar arası tekil.
    string ToolName { get; }

    // Router'ın bu sub-agent'ı lazy tool olarak çağırmasını sağlar. collector leaf MCP sonuçlarını
    // toplar (→ reply.items). request = router'ın ilettiği kullanıcı isteği.
    AITool AsRouterTool(ToolResultCollector collector);
}
