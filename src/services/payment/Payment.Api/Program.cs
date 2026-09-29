var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

// Marten kalıcılık kurulumu → Extensions/MartenExtensions.cs
builder.AddPaymentMarten();

// Wolverine mesajlaşma kurulumu → Extensions/MessagingExtensions.cs (AddCachingAspect'ten ÖNCE)
builder.AddPaymentMessaging();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.PaymentRead,
    AuthorizationScopes.PaymentWrite);
// RFC 9728 keşif (metadata dokümanı + 401 challenge parametreleri) — dış agent OAuth zinciri.
// Dış-agent demeti yalnız payment.read (yazma demet dışı — data-model).
builder.Services.AddMcpResourceMetadata(builder.Configuration, "payment",
    AuthorizationScopes.PaymentRead);
// logout: `logout` MCP tool'unun Identity.Server agent-logout ucuna forward client'ı.
builder.Services.AddAgentLogoutClient(builder.Configuration);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// hosted-CF ödeme yapılandırması (config[...] magic-string yasak → tip'li POCO + ValidateOnStart).
builder.Services.AddOptions<PaymentOptions>().BindConfiguration(nameof(PaymentOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<PaymentOptions>(sp => sp.GetRequiredService<IOptions<PaymentOptions>>().Value);
// Payment.Api makine token'ı (payment-s2s client_credentials) — Customer merchant-key S2S çağrısı.
builder.Services.AddOptions<Payment.Api.Options.SagaAuth>().BindConfiguration(nameof(Payment.Api.Options.SagaAuth))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Payment.Api.Options.SagaAuth>(sp => sp.GetRequiredService<IOptions<SagaAuth>>().Value);
builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);

// Customer merchant-key S2S makine token'ı (customer.read) + PG hosted-payment istemcisi.
builder.Services.AddTransient<PaymentTokenHandler>();

var customerGrpcAddress = builder.Configuration["services:customer-api:https:0"]
    ?? builder.Configuration["services:customer-api:http:0"]
    ?? "https://customer-api";
builder.Services
    .AddGrpcClient<MerchantKeyService.MerchantKeyServiceClient>(o => o.Address = new Uri(customerGrpcAddress))
    .AddHttpMessageHandler<PaymentTokenHandler>();
builder.Services.AddScoped<MerchantKeyClient>();

// PG (dış DropShop) hosted-payment — X-Api-Key per-request (statik header YOK); tam URL istemcide (PgBaseUrl).
builder.Services.AddHttpClient<PgHostedPaymentClient>();

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("payment");

builder.Services.AddHttpContextAccessor();
builder.Services.AddGrpc();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

var app = builder.Build();
// AppHost WithHttpHealthCheck("/health") bu ucu yoklar (Development-only map).
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();

// PG callback (HMAC, scope yok) ucu.
app.AddPaymentIntentEndpoints(apiVersionSet);
// hosted-CF S2S — Order.Api canlı-intent + link isteği (payment.write). REST'ten gRPC'ye taşındı.
app.MapGrpcService<PaymentIntentGrpcService>().RequireAuthorization(AuthorizationScopes.PaymentWrite);

// MCP korumalı — kimliksiz istek 401 + resource_metadata challenge alır (dış agent keşfi).
app.MapMcp("/mcp").RequireAuthorization();
app.MapMcpResourceMetadata();

await app.RunAsync();