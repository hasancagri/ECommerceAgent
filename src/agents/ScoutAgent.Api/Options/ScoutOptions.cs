using System.ComponentModel.DataAnnotations;

namespace ScoutAgent.Api.Options;

// 007: Kâşif sohbet beyni yapılandırması (tip'li Options — IConfiguration'dan doğrudan okuma yasak).
public sealed class ScoutOptions
{
    // OpenAI modeli (tool-calling destekli). gpt-4o-mini = ucuz + sağlam tool-call.
    [Required]
    public string Model { get; set; } = "gpt-4o-mini";

    // OpenAI API key (sk-...). user-secrets / AppHost secret parametresiyle verilir — git'e GİRMEZ.
    [Required]
    public string ApiKey { get; set; } = default!;

    // Hafıza: modele giden ham mesaj penceresi (son N) + özet eşiği (mesaj sayısı aşınca damıt).
    public int WindowSize { get; set; } = 10;
    public int SummaryThreshold { get; set; } = 20;

    // 014: kötüye-kullanım/maliyet guard'ı. Tek mesaj uzunluğu + bağlama alınan en çok mesaj (tam
    // transkript normal kullanımda yeter; aşırı uzun sohbet/dev-metin OpenAI maliyetini/bağlamı şişirmesin).
    public int MaxMessageLength { get; set; } = 2000;
    public int MaxContextMessages { get; set; } = 60;

    // 020: rate limit. Anonim (IP) sıkı, login (sub) gevşek. Dakika = burst (A, in-memory);
    // gün = kalıcı cap (B, Marten). OpenAI hard-limit nihai tavan; bunlar adalet + tek-kullanıcı tavanı.
    public int AnonPerMinute { get; set; } = 5;
    public int AnonPerDay { get; set; } = 30;
    public int UserPerMinute { get; set; } = 20;
    public int UserPerDay { get; set; } = 200;
}

// 007: BC MCP uçları (fasat YOK — statik liste; adresler service discovery'den gelir). Config-güdümlü:
// yeni bir BC MCP ucu gelince buraya (ya da appsettings'e) TEK satır eklenir — McpToolSource kodu değişmez.
public sealed class McpEndpointsOptions
{
    public List<McpEndpoint> Endpoints { get; set; } =
    [
        // Açık (anon araç kutusunda): kitap arama/detay + kitap yorumları.
        new() { Name = "catalog", Url = "http://catalog-api/mcp", RequiresAuth = false },
        new() { Name = "reviews", Url = "http://reviews-api/mcp", RequiresAuth = false },
        // Kişisel (yalnız login araç kutusunda; uç scope doğrular).
        new() { Name = "basket", Url = "http://basket-api/mcp", RequiresAuth = true },
        new() { Name = "order", Url = "http://order-api/mcp", RequiresAuth = true },
        new() { Name = "library", Url = "http://library-api/mcp", RequiresAuth = true },
        new() { Name = "customer", Url = "http://customer-api/mcp", RequiresAuth = true },
        new() { Name = "personalization", Url = "http://personalization-api/mcp", RequiresAuth = true },
    ];
}

// Tek MCP ucu tanımı. RequiresAuth=true → kişisel (girişsiz araç kutusuna KONMAZ + sunucu scope ister).
public sealed class McpEndpoint
{
    public string Name { get; set; } = default!;
    public string Url { get; set; } = default!;
    public bool RequiresAuth { get; set; }
}
