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
    AuthorizationScopes.PaymentWrite,
    AuthorizationScopes.OpsDeadletter);
// RFC 9728 keşif (metadata dokümanı + 401 challenge parametreleri) — dış agent OAuth zinciri.
// Dış-agent demeti yalnız payment.read (yazma demet dışı — data-model).
builder.Services.AddMcpResourceMetadata(builder.Configuration, "payment",
    AuthorizationScopes.PaymentRead,
    AuthorizationScopes.OpsDeadletter);
// logout: `logout` MCP tool'unun Identity.Server agent-logout ucuna forward client'ı.
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("payment");

builder.Services.AddHttpContextAccessor();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    // 088: ölü-mesaj operatör tool'ları Common'da — açık kayıt.
    .WithTools<Common.Utils.DeadLetters.DeadLetterAdminMcpTools>();

// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

var app = builder.Build();
// AppHost WithHttpHealthCheck("/health") bu ucu yoklar (Development-only map).
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();

// 093 PG pivotu: hosted-CF REST callback + Order S2S intent gRPC söküldü; yeni pay-by-link
// kontratı gelince ödeme girişi burada yeniden kurulur.

// MCP korumalı — kimliksiz istek 401 + resource_metadata challenge alır (dış agent keşfi).
app.MapMcp("/mcp").RequireAuthorization();
app.MapMcpResourceMetadata();

await app.RunAsync();