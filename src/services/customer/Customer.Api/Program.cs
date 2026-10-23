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
// RFC 9728 keşif (metadata dokümanı + 401 challenge parametreleri) — dış agent OAuth zinciri.
// fix: 062 adres YAZMA tool'ları açıldığında bu liste bayat kalmıştı — scope'unu PRM'den türeten
// istemciler (mcp-remote köprüsü) customer.write'sız token alıp add_address'te düşüyordu.
// /mcp TEK uç — PRM TAM demeti ilan eder.
builder.Services.AddMcpResourceMetadata(builder.Configuration, "customer",
    AuthorizationScopes.CustomerRead, AuthorizationScopes.CustomerWrite,
    AuthorizationScopes.OpsDeadletter);
// logout: `logout` MCP tool'unun Identity.Server agent-logout ucuna forward client'ı.
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("customer");

builder.Services.AddHttpContextAccessor();
builder.Services.AddGrpc();
// TEK korumalı uç /mcp — müşteri + merchant-admin tool'ları birlikte, tek assembly taramasıyla.
// Tool-görünürlük budaması KALDIRILDI; yetki tek katman: handler'daki [RequiredScope] (403 son savunma).
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    // 088: ölü-mesaj operatör tool'ları Common'da — açık kayıt.
    .WithTools<Common.Utils.DeadLetters.DeadLetterAdminMcpTools>();

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

// MCP korumalı — kimliksiz istek 401 + resource_metadata challenge alır (dış agent keşfi).
// TEK uç — merchant admin tool'lar scope-budamalı aynı ucta (scope katmanı handler'larda). /mcp-admin öldü.
app.MapMcp("/mcp");
app.MapMcpResourceMetadata();

await app.RunAsync();