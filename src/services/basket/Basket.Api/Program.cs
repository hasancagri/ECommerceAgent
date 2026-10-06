var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Marten kalıcılık kurulumu → Extensions/MartenExtensions.cs
builder.AddBasketMarten();

// Wolverine mesajlaşma kurulumu → Extensions/MessagingExtensions.cs (AddCachingAspect'ten ÖNCE).
builder.AddBasketMessaging();


builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.BasketRead,
    AuthorizationScopes.BasketWrite,
    // 088: ölü-mesaj operatör tool'ları (admin_list/get/replay/discard_dead_letter).
    AuthorizationScopes.OpsDeadletter);
// Dış tüketiciler icin opak UserKey (X-User-Key) custom auth semasi. JWT'ye dokunmaz.
builder.Services.AddApiKeyAuthentication(builder.Configuration);
// RFC 9728 keşif (metadata dokümanı + 401 challenge parametreleri) — dış agent OAuth zinciri.
builder.Services.AddMcpResourceMetadata(builder.Configuration, "basket",
    AuthorizationScopes.BasketRead, AuthorizationScopes.BasketWrite, AuthorizationScopes.OpsDeadletter);
// logout: `logout` MCP tool'unun Identity.Server agent-logout ucuna forward client'ı.
builder.Services.AddAgentLogoutClient(builder.Configuration);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("basket");

builder.Services.AddHttpContextAccessor();

// checkout saga ClearBasket gRPC sunucusu (Order saga'si makine token'iyla cagirir).
builder.Services.AddGrpc();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    // 088: ölü-mesaj operatör tool'ları Common'da (assembly taraması dışı) — açık kayıt.
    .WithTools<Common.Utils.DeadLetters.DeadLetterAdminMcpTools>();

var app = builder.Build();
// AppHost WithHttpHealthCheck("/health") bu ucu yoklar (Development-only map).
app.MapDefaultEndpoints();

app.UseAuthentication();
// X-User-Key varsa eager cozer: gecersiz->401, gecerli->principal (UseAuthorization'dan once).
app.UseApiKeyAuthentication();
app.UseAuthorization();


// MCP korumalı — kimliksiz istek 401 + resource_metadata challenge alır (dış agent keşfi).
app.MapMcp("/mcp").RequireAuthorization();
app.MapMcpResourceMetadata();

// GetBasketItems gRPC ucu (Order.Api chat siparis tamamlama; makine token'i basket.read).
app.MapGrpcService<Basket.Api.Grpc.Checkout.BasketItemsGrpcService>()
    .RequireAuthorization(AuthorizationScopes.BasketRead);

await app.RunAsync();