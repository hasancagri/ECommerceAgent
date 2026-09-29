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
    // Vault merchant kimliği yönetimi (admin-only capability).
    AuthorizationScopes.MerchantCredentialsWrite);
// RFC 9728 keşif (metadata dokümanı + 401 challenge parametreleri) — dış agent OAuth zinciri.
// fix: 062 adres YAZMA tool'ları açıldığında bu liste bayat kalmıştı — scope'unu PRM'den türeten
// istemciler (mcp-remote köprüsü) customer.write'sız token alıp add_address'te düşüyordu.
// /mcp TEK uç — PRM TAM demeti ilan eder (merchant.credentials.write dahil, contracts/mcp-surface.md).
builder.Services.AddMcpResourceMetadata(builder.Configuration, "customer",
    AuthorizationScopes.CustomerRead, AuthorizationScopes.CustomerWrite, AuthorizationScopes.MerchantCredentialsWrite);
// logout: `logout` MCP tool'unun Identity.Server agent-logout ucuna forward client'ı.
builder.Services.AddAgentLogoutClient(builder.Configuration);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// DropShop onboarding config (section "DropShopOnboarding"). (076: DropShopVault/kart config söküldü.)
builder.Services.AddOptionsExt();

// D3: PG onboarding S2S REST istemcisi — makine kimliği (client_credentials) handler'ıyla
// (070'in imperatif MCP sapması US4'te SÖKÜLDÜ; kontrat specs/078/contracts/pg-onboarding-rest.md).
builder.Services.AddTransient<Customer.Api.Onboarding.OnboardingGatewayTokenHandler>();
builder.Services.AddHttpClient<Customer.Api.Onboarding.PgOnboardingClient>()
    .AddHttpMessageHandler<Customer.Api.Onboarding.OnboardingGatewayTokenHandler>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

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
    .WithToolsFromAssembly();

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
// Payment.Api PG hosted-payment X-Api-Key kaynağı S2S çeker (customer.read). REST'ten gRPC'ye taşındı.
app.MapGrpcService<Customer.Api.Grpc.HostedPayment.MerchantKeyGrpcService>()
    .RequireAuthorization(AuthorizationScopes.CustomerRead);

// hosted credential-giriş ekranı — ANONİM (token = yetki; İlke V v1.11.1 capability-link istisnası).
app.MapCredentialEntryEndpoints();

// MCP korumalı — kimliksiz istek 401 + resource_metadata challenge alır (dış agent keşfi).
// TEK uç — merchant admin tool'lar scope-budamalı aynı ucta (scope katmanı handler'larda). /mcp-admin öldü.
app.MapMcp("/mcp").RequireAuthorization();
app.MapMcpResourceMetadata();

await app.RunAsync();