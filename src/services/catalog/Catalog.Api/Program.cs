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

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// 091 JSON seed'i SÖKÜLDÜ (098 yolu: katalog girişi Excel import'la gelecek).

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("catalog");

builder.Services.AddHttpContextAccessor();
// MCP yüzeyi SÖKÜLDÜ (tertemiz sayfa) — tüm /mcp tool'ları + DLQ MCP kaldırıldı; okuma/yazma REST first-party'ye.

// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

// 004 Dilim-1 admin anahtarı (ikinci kat — FR-014; Dilim 2'de JWT'ye evrilir, sökülür).
builder.Services.AddOptions<AdminApiKeyOptions>()
    .BindConfiguration(nameof(AdminApiKeyOptions))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AdminApiKeyOptions>>().Value);
builder.Services.AddScoped<AdminApiKeyEndpointFilter>();

// 004 (durum-güdümlü import): worker ImportJob tablosunu yoklar — Queued=dry-run, Confirmed=yazım.
// Mesaj/kuyruk tetiği YOK (kullanıcı kararı); Wolverine keşfiyle ilgisiz, düz hosted service.
builder.Services.AddHostedService<Catalog.Api.Process.ImportJobWorker>();

var app = builder.Build();
// AppHost WithHttpHealthCheck("/health") bu ucu yoklar (Development-only map).
app.MapDefaultEndpoints();

// Temiz-sayfa sökümünde JWT auth kaydı (AddAuthenticationAndAuthorizationExtension) MCP yüzeyiyle
// birlikte gitti; kayıtsız UseAuthentication/UseAuthorization açılışta fırlatır (096 canlı koşusunda
// yakalandı). Kalan tek şema ApiKey (S2S) — basket/order/payment ile aynı desen.
app.UseApiKeyAuthentication();

// 004: ilk REST admin yüzeyi — Excel import uçları (gateway /api/admin/catalog/** rotasından gelir).
app.MapImportJobEndpoints();

await app.RunAsync();