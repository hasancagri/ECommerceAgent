using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ScoutAgent.Api.Agents;

// 007/019: ScoutAgent BC MCP uçlarına DOĞRUDAN bağlanır (fasat YOK). Uç listesi CONFIG-GÜDÜMLÜ
// (McpEndpointsOptions). 019: sub-agent'lar GetClientAsync ile kendi ucunun client'ını alıp tipli
// wrapper'da CallToolAsync çağırır (seçilen sub-agent yalnız kendi ucuna bağlanır — kör toplama yok).
// Her HTTP isteği UserTokenForwardingHandler ile kullanıcı token'ını taşır → kişisel uçlar scope ister
// (gerçek sınır); token yoksa downstream 401 → wrapper structuredContent null → nazik "giriş gerekli".
public sealed class McpToolSource(
    McpEndpointsOptions options,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : ISingletonDependency
{
    // Program.cs AddHttpClient bu adla kaydeder — tek kaynak (typo'lu çift sessizce koparır).
    public const string HttpClientName = "mcp";

    // 019: MCP sonucu BC'den PascalCase/camelCase gelebilir → case-insensitive deserialize (sub-agent'lar
    // kullanır: structuredContent.Deserialize<SomutTip>(JsonOptions)). Reflection YOK; T derleme-zamanı.
    public static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    // 019 FIX: MCP tool sonucunu tipli nesneye çöz. MCP SDK 2.3.1 (+ bu BC'ler) çıktıyı `structuredContent`
    // DEĞİL `content[0]` TextContent JSON'u olarak döner → yalnız StructuredContent okuyan eski yol hep null
    // alıyordu (tüm sohbet kartları boş). Önce structuredContent (ileride dolarsa), yoksa ilk TextContent
    // JSON'u parse. Sub-agent wrapper'ları bunu çağırır (sniff/reflection yok; T derleme-zamanı).
    public static T? DeserializeResult<T>(CallToolResult result) where T : class
    {
        if (result.StructuredContent is { } sc && sc.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            return sc.Deserialize<T>(JsonOptions);
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        return string.IsNullOrWhiteSpace(text) ? null : System.Text.Json.JsonSerializer.Deserialize<T>(text, JsonOptions);
    }

    // 019: bir uca MCP client ver — sub-agent tool wrapper'ı CallToolAsync'i bunun üstünde çağırır
    // (tipli wrapper deseni: wrapper girdiyi/çıktı tipini bilir, sniff/reflection yok). Her çağrı taze.
    public async Task<McpClient> GetClientAsync(string endpointName, CancellationToken ct)
    {
        var endpoint = options.Endpoints.FirstOrDefault(e => e.Name == endpointName)
            ?? throw new InvalidOperationException($"Bilinmeyen MCP ucu: {endpointName}");
        return await CreateClientAsync(endpoint, ct);
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
