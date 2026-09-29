var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Cluster adresleri Aspire service discovery adlari (http://catalog-api gibi).
// AddServiceDiscoveryDestinationResolver: YARP bu adlari ServiceDefaults'in service
// discovery'si uzerinden (services__<ad>__http__0) gercek endpoint'e cozer.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();


builder.Services.AddAuthenticationAndAuthorizationExtension(builder.Configuration);

// ClientCredential policy söküldü (tek kullanan catalog-route REST proxy'si kalktı — MCP-only yüzey).
// Kalan "Password" policy = kullanıcı token'i şartı (gerekirse route'larda kullanılır; grant-tipi
// ayrıştırması ertelenmiş auth işine ait).
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Password", policy => policy.RequireAuthenticatedUser());
});

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapReverseProxy();
app.MapGet("/", () => "YARP (Gateway)");
app.UseAuthentication();
app.UseAuthorization();
await app.RunAsync();