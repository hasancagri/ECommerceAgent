using Catalog.Api.Filters;

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

// 012: OPSİYONEL JWT (007 deseni) — arama ucu login kullanıcıyı token'dan çözer (UserSearched capture).
// Anonim serbest (arama ucunda RequireAuthorization YOK). ValidateAudience=false: SPA access-token
// audience taşımaz (yalnız scope) → Common.AddAuthenticationAndAuthorizationExtension (ValidateAudience=
// true) REDDEDER, o yüzden inline. Issuer/imza/süre doğrulanır. ApiKey şeması (S2S/admin) yanında yaşar.
var identity = builder.Configuration.GetSection(nameof(IdentityOption)).Get<IdentityOption>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = identity.Address;
        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddTransient<ICurrentUser, CurrentUser>();

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

// 011: hibrit arama şema nesneleri (ParadeDB eklenti + BM25 index + product_embedding tablo/HNSW).
// AddCatalogMarten'DEN SONRA kaydedilir → Marten'in şema-apply hosted service'inden SONRA koşar
// (mt_doc_product kurulmuş olur); idempotent ham DDL.
builder.Services.AddHostedService<Catalog.Api.Search.ProductSearchSchemaInitializer>();

// 011 US2/US3: sorgu-anı embedding (OpenAI). Anahtar OPSİYONEL (yoksa semantik ayak devre-dışı,
// BM25 tam çalışır) → ValidateOnStart YOK. HttpClient fabrikası QueryEmbeddingClient için.
builder.Services.AddHttpClient();
builder.Services.AddOptions<Catalog.Api.Search.Embedding.OpenAiEmbeddingOptions>()
    .BindConfiguration(nameof(Catalog.Api.Search.Embedding.OpenAiEmbeddingOptions));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Catalog.Api.Search.Embedding.OpenAiEmbeddingOptions>>().Value);

var app = builder.Build();
// AppHost WithHttpHealthCheck("/health") bu ucu yoklar (Development-only map).
app.MapDefaultEndpoints();

// 012: opsiyonel JWT şeması yukarıda kaydedildi → UseAuthentication/UseAuthorization güvenli (eski
// "kayıtsız şema açılışta fırlatır" tuzağı artık geçerli değil). ApiKey (S2S/admin) şemasıyla yan yana.
app.UseAuthentication();
app.UseAuthorization();
app.UseApiKeyAuthentication();

// 004: ilk REST admin yüzeyi — Excel import uçları (gateway /api/admin/catalog/** rotasından gelir).
app.MapImportJobEndpoints();

// 011: hibrit arama uçları — müşteri arama/benzer (anonim) + admin embedding ingest (anahtar).
app.MapProductEndpoints();

await app.RunAsync();