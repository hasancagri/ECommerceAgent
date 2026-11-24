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
// 014: query-only MCP ucu GERİ (ADR adr-mcp-inward-agent — first-party agent-içi MCP, İlke I meşru
// kanal; dış/BYO yüzey kapalı). YALNIZ ScoutAgent tüketir; anonim kitap arama (search_books; 018'de
// get_book SÖKÜLDÜ). Tool'lar assembly taramasıyla bulunur (Features/Agents/Queries/*.cs [McpServerToolType]).
builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();

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

// 021 similar_books boost katsayıları (opsiyonel section — yoksa Options default'ları). Tüketici düz T.
builder.Services.AddOptions<Catalog.Api.Domains.Products.SimilarBooksOptions>()
    .BindConfiguration(nameof(Catalog.Api.Domains.Products.SimilarBooksOptions));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Catalog.Api.Domains.Products.SimilarBooksOptions>>().Value);

// 004 (durum-güdümlü import): worker ImportJob tablosunu yoklar — Queued=dry-run, Confirmed=yazım.
// Mesaj/kuyruk tetiği YOK (kullanıcı kararı); Wolverine keşfiyle ilgisiz, düz hosted service.
builder.Services.AddHostedService<Catalog.Api.Process.ImportJobWorker>();

// 018: arama şema nesneleri (ParadeDB pg_search eklenti + BM25 index; 018 söküm: embedding tablo/HNSW
// idempotent DROP). AddCatalogMarten'DEN SONRA kaydedilir → Marten'in şema-apply hosted service'inden
// SONRA koşar (mt_doc_product kurulmuş olur); idempotent ham DDL.
builder.Services.AddHostedService<Catalog.Api.Search.ProductSearchSchemaInitializer>();

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

// 014: anonim MCP ucu (ScoutAgent client olarak bağlanır). RequireAuthorization YOK — anonim gezinme meşru.
app.MapMcp("/mcp");

await app.RunAsync();