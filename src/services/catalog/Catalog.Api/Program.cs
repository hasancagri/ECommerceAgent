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
    Catalog.Api.Mcp.CatalogAdminSurface.SCOPES);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// 083 D6/T010: Excel yükleme ekranı config'i (link tabanı + ömür) — section "ImportOptions".
// Tüketici düz T enjekte eder (078 emsali; IOptions<T> değil).
builder.Services.AddOptions<Catalog.Api.Options.ImportOptions>()
    .BindConfiguration(nameof(Catalog.Api.Options.ImportOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Catalog.Api.Options.ImportOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Catalog.Api.Options.ImportOptions>>().Value);

// 083 T013/FR-002+FR-004: bekleyen staging satırlarını arka planda ürüne çeviren dayanıklı süreç
// (Process/ deseni). 051 books.json seeder'ı söküldü (FR-011) — Excel import tek katalog giriş yolu.
builder.Services.AddHostedService<Catalog.Api.Import.ImportProcessor>();

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("catalog");

builder.Services.AddHttpContextAccessor();
// 085 R1: TEK uç /mcp (/mcp-admin öldü). Oturum başına TAZE options (SDK, ConfigureSessionOptions
// verilince IOptionsFactory'den yeni kurar); tool seti isteği ATAN TOKEN'IN SCOPE'una göre budanır
// (yol-prefix değil): admin tool'lar (CatalogAdminSurface.ToolScopeMap) YALNIZ ilgili scope varsa görünür.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(http => http.ConfigureSessionOptions = (ctx, opts, _) =>
    {
        var tools = opts.ToolCollection;
        if (tools is null)
            return Task.CompletedTask;
        foreach (var tool in tools
                     .Where(t => !McpScopePruningExtension.IsToolVisible(
                         t.ProtocolTool.Name, Catalog.Api.Mcp.CatalogAdminSurface.TOOL_SCOPE_MAP, ctx.User)).ToArray())
            tools.Remove(tool);
        return Task.CompletedTask;
    })
    .WithToolsFromAssembly();

// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

var app = builder.Build();
// AppHost WithHttpHealthCheck("/health") bu ucu yoklar (Development-only map).
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();

// 074: domain iş REST yüzeyi söküldü — catalog admin/okuma tümüyle MCP (/mcp).
// Ürün girişi = Excel import (083) + admin_create_product (MCP). Kalan REST = MCP-infra + import ekranı.

// 083 US1/FR-001: hosted xlsx yükleme ekranı — ANONİM (token = yetki; İLKE V v1.11.1 capability-link
// istisnası). MapMcp'DEN ÖNCE map'lenir; auth token URL'inde taşınır (078 emsali).
app.MapImportUploadEndpoints();

// 085: TEK uç — anonim keşif + admin tool'lar scope-budamalı; scope katmanı handler'larda, tool-bazlı
// ([RequiredScope(AdminCatalogWrite)] vb., 403 son savunma). /mcp-admin öldü.
app.MapMcp("/mcp");

await app.RunAsync();