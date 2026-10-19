var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcılık (Marten + şema/index + Wolverine entegrasyonu) → Extensions/MartenExtensions.cs.
builder.AddCatalogMarten();

// Mesajlaşma (Wolverine + RabbitMQ topoloji + handler keşfi) → Extensions/MessagingExtensions.cs.
// SIRA: AddCachingAspect'ten ÖNCE (cache aspect IMessageBus'ı sarar).
builder.AddCatalogMessaging();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// Admin yüzeyi (TEK /mcp'de, scope-budamalı) scope demeti = Catalog.Api.Mcp.CatalogAdminSurface.Scopes (okuma + yazma).
builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    [.. Catalog.Api.Mcp.CatalogAdminSurface.SCOPES, AuthorizationScopes.OpsDeadletter]);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// 091: açılış katalog seed'i config'i (yol + anahtar) — section "SeedOptions"; yol AppHost env'den.
// Tüketici düz T enjekte eder (IOptions<T> değil).
builder.Services.AddOptions<Catalog.Api.Options.SeedOptions>()
    .BindConfiguration(nameof(Catalog.Api.Options.SeedOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Catalog.Api.Options.SeedOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Catalog.Api.Options.SeedOptions>>().Value);

// 091: commit'li books.json'dan tek-seferlik açılış seed'i (051 deseninin dönüşü; Excel import söküldü).
builder.Services.AddHostedService<Catalog.Api.Seeding.BookSeedHostedService>();

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("catalog");

builder.Services.AddHttpContextAccessor();
// TEK uç /mcp — müşteri + admin tool'ları tek assembly taramasıyla kaydedilir. Tool-görünürlük budaması
// KALDIRILDI (admin MCP ayrımı sökülü); yetki tek katman: handler'daki [RequiredScope] (403 son savunma).
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

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();

// domain iş REST yüzeyi söküldü — catalog admin/okuma tümüyle MCP (/mcp).
// Ürün girişi = açılış seed'i (091, books.json) + admin_create_product (MCP). Kalan REST = MCP-infra.

// TEK uç — anonim keşif + admin tool'lar scope-budamalı; scope katmanı handler'larda, tool-bazlı
// ([RequiredScope(AdminCatalogWrite)] vb., 403 son savunma). /mcp-admin öldü.
app.MapMcp("/mcp");

await app.RunAsync();