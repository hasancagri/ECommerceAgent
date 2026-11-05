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

var app = builder.Build();
app.MapDefaultEndpoints();

// Kenar yetki: admin rota sınıfı anahtar ister; eşleşmeyen istek proxy'ye düşmeden 401 (FR-013).
app.UseMiddleware<AdminApiKeyMiddleware>();

app.MapReverseProxy();

await app.RunAsync();
