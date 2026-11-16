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

// 007: React SPA (dev :5173) tek origin Gateway'e cross-origin XHR atar (chat + Authorization bearer).
// Dev allowlist; prod origin'i config'e taşınır. Preflight için auth middleware'den ÖNCE.
const string SpaCorsPolicy = "spa";
builder.Services.AddCors(options => options.AddPolicy(SpaCorsPolicy, policy => policy
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseCors(SpaCorsPolicy);

// Kenar yetki: admin rota sınıfı anahtar ister; eşleşmeyen istek proxy'ye düşmeden 401 (FR-013).
app.UseMiddleware<AdminApiKeyMiddleware>();

app.MapReverseProxy();

await app.RunAsync();
