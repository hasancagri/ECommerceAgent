var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

// Marten + Wolverine kurulumu Extensions/'a taşındı (Program.cs orkestrasyon dışı; catalog aynası).
builder.AddCustomerMarten();
builder.AddCustomerMessaging();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.CustomerRead,
    AuthorizationScopes.CustomerWrite,
    // 088: ölü-mesaj operatör tool'ları (admin_list/get/replay/discard_dead_letter).
    AuthorizationScopes.OpsDeadletter);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("customer");

builder.Services.AddHttpContextAccessor();
builder.Services.AddGrpc();

// 014: query-only MCP ucu GERİ (first-party agent-içi MCP, İlke I meşru kanal; dış/BYO yüzey kapalı).
// YALNIZ ScoutAgent tüketir; get_my_addresses (kullanıcının adres defteri, customer.read scope).
// Tool'lar assembly taramasıyla bulunur (Features/Agents/Queries/*.cs [McpServerToolType]).
builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();

var app = builder.Build();
// AppHost WithHttpHealthCheck("/health") bu ucu yoklar (Development-only map).
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseAuthorization();

// payment-context internal ucu SÖKÜLDÜ (kart-saklama gitti).
// Order.Api hosted-CF ödemesi varsayılan adresi S2S çeker (customer.read).
// performans için REST'ten gRPC'ye taşındı (İlke I — BC-arası S2S artık gRPC, dış webhook hariç).
app.MapGrpcService<Customer.Api.Grpc.HostedPayment.AddressGrpcService>()
    .RequireAuthorization(AuthorizationScopes.CustomerRead);

// 014: scope-korumalı MCP ucu (ScoutAgent JWT ile bağlanır; kişisel adres verisi — customer.read).
app.MapMcp("/mcp").RequireAuthorization(AuthorizationScopes.CustomerRead);

await app.RunAsync();