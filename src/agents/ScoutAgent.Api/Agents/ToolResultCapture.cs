using System.Text.Json;
using Microsoft.Extensions.AI;

namespace ScoutAgent.Api.Agents;

// 014: tur boyunca çağrılan MCP tool'larının HAM sonucunu yakalar. Gerekçe: MAF `UseFunctionInvocation`
// tool-sonucu mesajlarını araya-katman İÇİNDE tüketip LLM'e besliyor; nihai `AgentRunResponse` genelde
// yalnız son asistan metnini taşıyor → `items` boş kalıyor (kart/resim/buton render edilemiyor).
// Çözüm: her tool'u sarıp çıktıyı burada biriktir, SendMessage bunu `reply.items`'a koysun.
// Her SendMessage turu için YENİ örnek (request-scoped; thread-safe değil — tek tur sıralı çalışır).
public sealed class ToolResultCollector
{
    private readonly List<(string Tool, object? Result)> _results = [];
    public void Add(string tool, object? result) => _results.Add((tool, result));
    public IReadOnlyList<(string Tool, object? Result)> Results => _results;
}

// AIFunction delegating sarmalayıcı — inner tool'u çağırır, sonucu collector'a yazar, sonucu AYNEN döner
// (LLM akışını hiç değiştirmez; yalnız yan-kayıt). Ad/şema/açıklama inner'dan aynen aktarılır.
public sealed class CapturingFunction(AIFunction inner, ToolResultCollector collector) : AIFunction
{
    public override string Name => inner.Name;
    public override string Description => inner.Description;
    public override JsonElement JsonSchema => inner.JsonSchema;
    public override JsonElement? ReturnJsonSchema => inner.ReturnJsonSchema;
    public override IReadOnlyDictionary<string, object?> AdditionalProperties => inner.AdditionalProperties;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var result = await inner.InvokeAsync(arguments, cancellationToken);
        collector.Add(inner.Name, result);
        return result;
    }
}
