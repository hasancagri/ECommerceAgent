namespace ScoutAgent.Api.Agents;

// 019: tur boyunca sub-agent tool wrapper'larının ürettiği TİPLİ sonuçları biriktirir (SendMessage
// bunu reply.items'a koyar). Her wrapper `Add(toolName, typedResult)` çağırır (MCP structuredContent
// zaten Deserialize<SomutTip> edilmiş). Her SendMessage turu için YENİ örnek (request-scoped; tek tur
// sıralı → thread-safe değil). Sıra = çağrı sırası (D5).
public sealed class ToolResultCollector
{
    private readonly List<(string Tool, object? Result, object? Filters)> _results = [];
    // filters: yalnız search_books doldurur (kullanılan arama args'ı) — React sayfalama için saklar.
    public void Add(string tool, object? result, object? filters = null) => _results.Add((tool, result, filters));
    public IReadOnlyList<(string Tool, object? Result, object? Filters)> Results => _results;
}
