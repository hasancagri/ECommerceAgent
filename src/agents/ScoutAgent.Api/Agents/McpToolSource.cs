using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace ScoutAgent.Api.Agents;

// 007 FR-005a: ScoutAgent her BC MCP ucuna DOĞRUDAN bağlanır (fasat/keşif katmanı YOK). Uç listesi
// CONFIG-GÜDÜMLÜ (McpEndpointsOptions) — yeni BC = tek config satırı, bu sınıf DEĞİŞMEZ. TÜM uçlar her
// istekte toplanır (oturum-bağımsız, cache YOK — her istek taze client). Her tool çağrısının HTTP
// isteği UserTokenForwardingHandler ile kullanıcı token'ını taşır → kişisel uçlar her istekte scope
// ister (gerçek sınır); token yoksa downstream 401 → o uç atlanır (model "giriş gerekli" der).
public sealed class McpToolSource(
    McpEndpointsOptions options,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : ISingletonDependency
{
    // Program.cs AddHttpClient bu adla kaydeder — tek kaynak (typo'lu çift sessizce koparır).
    public const string HttpClientName = "mcp";

    // collector: tool çıktılarını yakalamak için (SendMessage verir — reply.items oradan dolar).
    public async Task<IList<AITool>> GetToolsAsync(ToolResultCollector collector, CancellationToken ct)
    {
        // Uçlara PARALEL bağlan — süre = en yavaş TEK uç (sıralı toplam DEĞİL). Ölü/yavaş uç (401/404 +
        // SSE fallback) diğerlerini bekletmez. Her uç kendi hatasını yutar (FR-008).
        var perEndpoint = await Task.WhenAll(options.Endpoints.Select(async endpoint =>
        {
            try
            {
                var client = await CreateClientAsync(endpoint, ct);
                var endpointTools = await client.ListToolsAsync(cancellationToken: ct);
                // Her tool'u yakalayıcıyla sar — çıktı collector'a yazılır (AIFunction olanlar; değilse aynen).
                return endpointTools
                    .Select(t => t is AIFunction f ? (AITool)new CapturingFunction(f, collector) : t)
                    .ToList();
            }
            catch (Exception ex)
            {
                // Bir uç erişilemezse (BC down / token yoksa 401) o aracı atla — diğerleri çalışır (FR-008).
                loggerFactory.CreateLogger<McpToolSource>()
                    .LogWarning(ex, "MCP ucu erişilemedi: {Endpoint}", endpoint.Name);
                return new List<AITool>();
            }
        }));

        return perEndpoint.SelectMany(x => x).ToList();
    }

    private async Task<McpClient> CreateClientAsync(McpEndpoint endpoint, CancellationToken ct)
    {
        // Tek "mcp" client (UserTokenForwardingHandler ekli — Program.cs): isteğin bearer'ını taşır,
        // yoksa düz gider. Service discovery çözümlü (ServiceDefaults).
        var httpClient = httpClientFactory.CreateClient(HttpClientName);

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
