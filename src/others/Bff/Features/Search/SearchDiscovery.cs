using System.Text.Json;

namespace Bff.Features.Search;

// 012: keşif arama orkestrasyonu. Catalog public REST aramasını (011) çağırır, sonucu istemciye AYNEN
// döner; login ise UserSearched yayınlar (anonim → yayınlamaz). Publish arama sonucunu ETKİLEMEZ (FR-006):
// Catalog yanıtı önce döner, publish ayrık + hata-yutar. BFF durumsuz — kendi verisi yok.
public static class SearchDiscovery
{
    public record SearchRequest(string? Query, string? Mode, int? Limit);

    public static void MapDiscoverySearch(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/discovery/search", async (
            SearchRequest body,
            IHttpClientFactory httpFactory,
            IMessageBus bus,
            ICurrentUser currentUser,
            HttpContext http,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var query = body.Query ?? string.Empty;
            var mode = string.IsNullOrWhiteSpace(body.Mode) ? "keyword" : body.Mode!.Trim();
            var limit = body.Limit is > 0 ? body.Limit.Value : 20;

            // 1) Catalog aramasını çağır (service discovery), sonucu aynen döndür.
            var catalog = httpFactory.CreateClient("catalog");
            var path = $"/api/catalog/products/search?q={Uri.EscapeDataString(query)}&mode={Uri.EscapeDataString(mode)}&limit={limit}";
            var response = await catalog.GetAsync(path, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return Results.Content(json, "application/json", statusCode: (int)response.StatusCode);

            // 2) Login ise UserSearched yayınla (publish aramayı bloklamaz/etkilemez — FR-006).
            currentUser.Load(http.User);
            if (currentUser.Id != Guid.Empty && !string.IsNullOrWhiteSpace(query))
            {
                try
                {
                    await bus.PublishAsync(new IntegrationEvents.UserSearched(
                        currentUser.Id, query.Trim(), mode, CountResults(json), DateTimeOffset.UtcNow));
                }
                catch (Exception ex)
                {
                    loggerFactory.CreateLogger("SearchDiscovery")
                        .LogWarning(ex, "012 UserSearched yayını başarısız (arama sonucu etkilenmez).");
                }
            }

            return Results.Content(json, "application/json");
        });
    }

    // Catalog arama yanıtı = ProductSummary dizisi; sonuç sayısı = dizi uzunluğu.
    private static int CountResults(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.GetArrayLength() : 0;
        }
        catch
        {
            return 0;
        }
    }
}
