using Gateway.Auth;
using Gateway.Options;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Rota/cluster tablosu config'te (contracts/gateway-routes.md) — Program.cs yalnız "ne kuruluyor".
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

builder.Services.AddOptions<GatewayAuthOptions>()
    .BindConfiguration(nameof(GatewayAuthOptions))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<GatewayAuthOptions>>().Value);

// 011: React dev istemcisi (Vite :5173) gateway'e cross-origin XHR atar (arama/keşif). Preflight
// için dev CORS — yalnız localhost:5173. Prod'da reverse-proxy aynı origin'de servis eder (gerekmez).
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();
app.MapDefaultEndpoints();

// CORS proxy'den ÖNCE (preflight OPTIONS admin-middleware'e/proxy'ye düşmeden yanıtlansın).
app.UseCors();

// Kenar yetki: admin rota sınıfı anahtar ister; eşleşmeyen istek proxy'ye düşmeden 401 (FR-013).
app.UseMiddleware<AdminApiKeyMiddleware>();

app.MapReverseProxy();

await app.RunAsync();
