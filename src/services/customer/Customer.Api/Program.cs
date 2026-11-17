var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

// Marten + Wolverine kurulumu Extensions/'a taÅÄ±ndÄ± (Program.cs orkestrasyon dÄ±ÅÄ±; catalog aynasÄ±).
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
    // 088: Ã¶lÃ¼-mesaj operatÃ¶r tool'larÄ± (admin_list/get/replay/discard_dead_letter).
    AuthorizationScopes.OpsDeadletter);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// L2 (paylaÅÄ±mlÄ±) Ã¶nbellek katmanÄ± â Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnÄ±z L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'Ä± Åeffaf sar. UseWolverine'den sonra olmalÄ±.
builder.Services.AddCachingAspect("customer");

builder.Services.AddHttpContextAccessor();
builder.Services.AddGrpc();

// 014: query-only MCP ucu GERÄ° (first-party agent-iÃ§i MCP, Ä°lke I meÅru kanal; dÄ±Å/BYO yÃ¼zey kapalÄ±).
// YALNIZ ScoutAgent tÃ¼ketir; get_my_addresses (kullanÄ±cÄ±nÄ±n adres defteri, customer.read scope).
// Tool'lar assembly taramasÄ±yla bulunur (Features/Agents/Queries/*.cs [McpServerToolType]).
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

// payment-context internal ucu SÃKÃLDÃ (kart-saklama gitti).
// Order.Api hosted-CF Ã¶demesi varsayÄ±lan adresi S2S Ã§eker (customer.read).
// performans iÃ§in REST'ten gRPC'ye taÅÄ±ndÄ± (Ä°lke I â BC-arasÄ± S2S artÄ±k gRPC, dÄ±Å webhook hariÃ§).
app.MapGrpcService<Customer.Api.Grpc.HostedPayment.AddressGrpcService>()
    .RequireAuthorization(AuthorizationScopes.CustomerRead);

// 014: scope-korumalÄ± MCP ucu (ScoutAgent JWT ile baÄlanÄ±r; kiÅisel adres verisi â customer.read).
app.MapMcp("/mcp").RequireAuthorization();  // 014: personal data, no named scope (013 pattern) - identity from sub

await app.RunAsync();