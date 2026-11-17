using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace ScoutAgent.Api.Agents;

// 007 FR-005a: ScoutAgent her BC MCP ucuna DOĞRUDAN bağlanır (fasat/keşif katmanı YOK). Uç listesi
// CONFIG-GÜDÜMLÜ (McpEndpointsOptions) — yeni BC = tek config satırı, bu sınıf DEĞİŞMEZ. Client'lar
// isimle cache'lenir (handshake bir kez); her tool çağrısının HTTP isteği UserTokenForwardingHandler
// ile kullanıcı token'ını taşır → kişisel uçlar her istekte scope ister (gerçek sınır). Araç kutusu
// gating: girişsiz oturumda RequiresAuth uçları HİÇ eklenmez (deneyim katmanı — model 403 görmez).
public sealed class McpToolSource(
    McpEndpointsOptions options,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : ISingletonDependency
{
    private readonly ConcurrentDictionary<string, Task<McpClient>> _clients = new();

    // Oturum durumuna göre araç kutusu: anonim = yalnız RequiresAuth=false uçlar; girişli = tümü.
    // collector: tool çıktılarını yakalamak için (SendMessage verir — reply.items oradan dolar).
    public async Task<IList<AITool>> GetToolsAsync(
        bool isAuthenticated, ToolResultCollector collector, CancellationToken ct)
    {
        var endpoints = options.Endpoints.Where(e => isAuthenticated || !e.RequiresAuth);

        // Uçlara PARALEL bağlan — süre = en yavaş TEK uç (sıralı toplam DEĞİL). Ölü/yavaş uç (401/404 +
        // SSE fallback) diğerlerini bekletmez. Her uç kendi hatasını yutar (FR-008).
        var perEndpoint = await Task.WhenAll(endpoints.Select(async endpoint =>
        {
            try
            {
                var client = await _clients.GetOrAdd(endpoint.Name, _ => CreateClientAsync(endpoint, ct));
                var endpointTools = await client.ListToolsAsync(cancellationToken: ct);
                // Her tool'u yakalayıcıyla sar — çıktı collector'a yazılır (AIFunction olanlar; değilse aynen).
                return endpointTools
                    .Select(t => t is AIFunction f ? (AITool)new CapturingFunction(f, collector) : t)
                    .ToList();
            }
            catch (Exception ex)
            {
                // Bir uç erişilemezse (BC down) o aracı atla — diğerleri çalışmaya devam (FR-008).
                _clients.TryRemove(endpoint.Name, out _);
                loggerFactory.CreateLogger<McpToolSource>()
                    .LogWarning(ex, "MCP ucu erişilemedi: {Endpoint}", endpoint.Name);
                return new List<AITool>();
            }
        }));

        return perEndpoint.SelectMany(x => x).ToList();
    }

    private async Task<McpClient> CreateClientAsync(McpEndpoint endpoint, CancellationToken ct)
    {
        // Kişisel uçlar token-taşıyan HttpClient kullanır (UserTokenForwardingHandler ekli — Program.cs);
        // anonim uç düz HttpClient. İkisi de service discovery çözümlü (ServiceDefaults).
        var httpClient = httpClientFactory.CreateClient(
            endpoint.RequiresAuth ? "mcp-authed" : "mcp-anon");

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(endpoint.Url),
                Name = endpoint.Name,
            },
            httpClient,
            loggerFactory);

        return await McpClient.CreateAsync(transport, cancellationToken: ct);
    }
}
