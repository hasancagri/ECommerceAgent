var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Marten kalÄ±cÄ±lÄ±k kurulumu â Extensions/MartenExtensions.cs
builder.AddBasketMarten();

// Wolverine mesajlaÅma kurulumu â Extensions/MessagingExtensions.cs (AddCachingAspect'ten ÃNCE).
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
    // 088: Ã¶lÃ¼-mesaj operatÃ¶r tool'larÄ± (admin_list/get/replay/discard_dead_letter).
    AuthorizationScopes.OpsDeadletter);
// DÄ±Å tÃ¼keticiler icin opak UserKey (X-User-Key) custom auth semasi. JWT'ye dokunmaz.
builder.Services.AddApiKeyAuthentication(builder.Configuration);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// L2 (paylaÅÄ±mlÄ±) Ã¶nbellek katmanÄ± â Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnÄ±z L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'Ä± Åeffaf sar. UseWolverine'den sonra olmalÄ±.
builder.Services.AddCachingAspect("basket");

builder.Services.AddHttpContextAccessor();

// 014: query-only MCP ucu (ScoutAgent tÃ¼ketir; first-party agent-iÃ§i MCP). YALNIZ okuma â
// get_basket kullanÄ±cÄ±nÄ±n kendi sepetini dÃ¶ner. Tool'lar assembly taramasÄ±yla bulunur
// (Features/Agents/Queries/*.cs [McpServerToolType]).
builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();

// checkout saga ClearBasket gRPC sunucusu (Order saga'si makine token'iyla cagirir).
builder.Services.AddGrpc();

var app = builder.Build();
// AppHost WithHttpHealthCheck("/health") bu ucu yoklar (Development-only map).
app.MapDefaultEndpoints();

app.UseAuthentication();
// X-User-Key varsa eager cozer: gecersiz->401, gecerli->principal (UseAuthorization'dan once).
app.UseApiKeyAuthentication();
app.UseAuthorization();


// GetBasketItems gRPC ucu (Order.Api chat siparis tamamlama; makine token'i basket.read).
app.MapGrpcService<Basket.Api.Grpc.Checkout.BasketItemsGrpcService>()
    .RequireAuthorization(AuthorizationScopes.BasketRead);

// 014: query-only MCP ucu â kullanÄ±cÄ±nÄ±n kendi sepeti (JWT zorunlu, basket.read scope).
app.MapMcp("/mcp").RequireAuthorization();  // 014: personal data, no named scope (013 pattern) - identity from sub

await app.RunAsync();