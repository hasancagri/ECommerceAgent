var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// KalÄ±cÄ±lÄ±k (Marten + Åema/index + Wolverine entegrasyonu) â Extensions/MartenExtensions.cs.
builder.AddOrderMarten();

// MesajlaÅma (Wolverine + RabbitMQ topoloji + handler keÅfi) â Extensions/MessagingExtensions.cs.
// SIRA: AddCachingAspect'ten ÃNCE (cache aspect IMessageBus'Ä± sarar).
builder.AddOrderMessaging();


builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.OrderRead,
    AuthorizationScopes.OrderWrite,
    // satin-alma kaniti gRPC ucu reviews.write ister (R4 â ayri scope acilmaz).
    AuthorizationScopes.ReviewsWrite,
    // 088: Ã¶lÃ¼-mesaj operatÃ¶r tool'larÄ± (admin_list/get/replay/discard_dead_letter).
    AuthorizationScopes.OpsDeadletter);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// House-style Options: appsettings section'lari tip'li POCO'ya bagla (config[...] magic-string yasak).
builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);
builder.Services.AddOptions<Checkout>().BindConfiguration(nameof(Checkout))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Checkout>(sp => sp.GetRequiredService<IOptions<Checkout>>().Value);
// hosted-CF start_payment makine token'Ä± (order-saga; basket.read + customer.read + payment.write).
builder.Services.AddOptions<Order.Api.Options.SagaAuth>().BindConfiguration(nameof(Order.Api.Options.SagaAuth))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Order.Api.Options.SagaAuth>(sp => sp.GetRequiredService<IOptions<Order.Api.Options.SagaAuth>>().Value);

// L2 (paylaÅÄ±mlÄ±) Ã¶nbellek katmanÄ± â Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnÄ±z L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'Ä± Åeffaf sar. UseWolverine'den sonra olmalÄ±.
builder.Services.AddCachingAspect("order");
builder.Services.AddHttpContextAccessor();

// 014: query-only MCP ucu GERÄ° (ADR adr-mcp-inward-agent â first-party agent-iÃ§i MCP, Ä°lke I meÅru
// kanal). YALNIZ ScoutAgent tÃ¼ketir; kiÅisel sipariÅ sorgusu (get_my_orders) â scope-korumalÄ±.
// Tool'lar assembly taramasÄ±yla bulunur (Features/Agents/Queries/*.cs [McpServerToolType]).
builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();

// hosted-CF start_payment S2S yolu â makine token (order-saga) + sepet gRPC + Payment/Customer istemcileri.
builder.Services.AddTransient<SagaTokenHandler>();

var basketGrpcAddress = builder.Configuration["services:basket-api:https:0"]
    ?? builder.Configuration["services:basket-api:http:0"]
    ?? "https://basket-api";
builder.Services
    .AddGrpcClient<Shared.Grpc.Basket.BasketQuery.BasketQueryClient>(o => o.Address = new Uri(basketGrpcAddress))
    .AddHttpMessageHandler<SagaTokenHandler>();
builder.Services.AddScoped<BasketItemsClientProxy>();

var customerGrpcAddress = builder.Configuration["services:customer-api:https:0"]
    ?? builder.Configuration["services:customer-api:http:0"]
    ?? "https://customer-api";
builder.Services
    .AddGrpcClient<Shared.Grpc.Customer.AddressQuery.AddressQueryClient>(o => o.Address = new Uri(customerGrpcAddress))
    .AddHttpMessageHandler<SagaTokenHandler>();
builder.Services.AddScoped<AddressClient>();

// Discount gRPC istemcisi SÃKÃLDÃ (095 branch'i) â kampanya/indirim modeli kalktÄ±; tutar = liste fiyatÄ±.

// 094 pay-by-link: Payment.Api S2S istemcisi â start_payment Ã¶deme niyeti + PG hosted link ister (payment.write).
var paymentGrpcAddress = builder.Configuration["services:payment-api:https:0"]
    ?? builder.Configuration["services:payment-api:http:0"]
    ?? "https://payment-api";
builder.Services
    .AddGrpcClient<PaymentIntentService.PaymentIntentServiceClient>(o => o.Address = new Uri(paymentGrpcAddress))
    .AddHttpMessageHandler<SagaTokenHandler>();
builder.Services.AddScoped<PaymentIntentClient>();

// MCP yÃ¼zeyi SÃKÃLDÃ (tertemiz sayfa) â /mcp sipariÅ/Ã¶deme-baÅlat tool'larÄ± + DLQ MCP kaldÄ±rÄ±ldÄ±; REST first-party'ye.

// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

var app = builder.Build();
// AppHost WithHttpHealthCheck("/health") bu ucu yoklar (Development-only map).
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();


// 094 pay-by-link: PG hosted Ã¶deme dÃ¶nÃ¼Å ucu (anonim bilgilendirme; gerÃ§ek durum webhook'tan).
app.AddCheckoutDoneEndpoint();

// 014: kiÅisel MCP ucu (ScoutAgent client olarak baÄlanÄ±r). KiÅisel veri â scope-korumalÄ±.
app.MapMcp("/mcp").RequireAuthorization();  // 014: personal data, no named scope (013 pattern) - identity from sub

await app.RunAsync();