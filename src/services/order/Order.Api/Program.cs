var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcılık (Marten + şema/index + Wolverine entegrasyonu) → Extensions/MartenExtensions.cs.
builder.AddOrderMarten();

// Mesajlaşma (Wolverine + RabbitMQ topoloji + handler keşfi) → Extensions/MessagingExtensions.cs.
// SIRA: AddCachingAspect'ten ÖNCE (cache aspect IMessageBus'ı sarar).
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
    // satin-alma kaniti gRPC ucu reviews.write ister (R4 — ayri scope acilmaz).
    AuthorizationScopes.ReviewsWrite,
    // 088: ölü-mesaj operatör tool'ları (admin_list/get/replay/discard_dead_letter).
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
// hosted-CF start_payment makine token'ı (order-saga; basket.read + customer.read + payment.write).
builder.Services.AddOptions<Order.Api.Options.SagaAuth>().BindConfiguration(nameof(Order.Api.Options.SagaAuth))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Order.Api.Options.SagaAuth>(sp => sp.GetRequiredService<IOptions<Order.Api.Options.SagaAuth>>().Value);

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("order");
builder.Services.AddHttpContextAccessor();

// hosted-CF start_payment S2S yolu — makine token (order-saga) + sepet gRPC + Payment/Customer istemcileri.
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

// Discount gRPC istemcisi SÖKÜLDÜ (095 branch'i) — kampanya/indirim modeli kalktı; tutar = liste fiyatı.

// 094 pay-by-link: Payment.Api S2S istemcisi — start_payment ödeme niyeti + PG hosted link ister (payment.write).
var paymentGrpcAddress = builder.Configuration["services:payment-api:https:0"]
    ?? builder.Configuration["services:payment-api:http:0"]
    ?? "https://payment-api";
builder.Services
    .AddGrpcClient<PaymentIntentService.PaymentIntentServiceClient>(o => o.Address = new Uri(paymentGrpcAddress))
    .AddHttpMessageHandler<SagaTokenHandler>();
builder.Services.AddScoped<PaymentIntentClient>();

// MCP yüzeyi SÖKÜLDÜ (tertemiz sayfa) — /mcp sipariş/ödeme-başlat tool'ları + DLQ MCP kaldırıldı; REST first-party'ye.

// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

var app = builder.Build();
// AppHost WithHttpHealthCheck("/health") bu ucu yoklar (Development-only map).
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();


// 094 pay-by-link: PG hosted ödeme dönüş ucu (anonim bilgilendirme; gerçek durum webhook'tan).
app.AddCheckoutDoneEndpoint();

await app.RunAsync();