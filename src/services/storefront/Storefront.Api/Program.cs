var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcılık (Marten + pgvector + şema/index + Wolverine entegrasyonu) → Extensions/MartenExtensions.cs.
builder.AddStorefrontMarten();

// Mesajlaşma (Wolverine + RabbitMQ topoloji + handler keşfi) → Extensions/MessagingExtensions.cs.
// SIRA: AddCachingAspect'ten ÖNCE (cache aspect IMessageBus'ı sarar).
builder.AddStorefrontMessaging();

// R1/R2: kısıtlı rol conn-string'i aşağıda gerekir; conn-string burada da yerelde okunur.
var storefrontDb = builder.Configuration.GetConnectionString("storefrontDb")!;

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.StorefrontRead);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// OpenAI embedding config — fail-fast (ApiKey yoksa açılmaz; ChatAgent emsali). Tüketici düz T enjekte eder.
builder.Services.AddOptions<OpenAiOption>().BindConfiguration("OpenAI")
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<OpenAiOption>>().Value);
// SemanticSearchOption söküldü (eşik prompt kalıbında); backfill batch ayarı dar option'da.
builder.Services.AddOptions<EmbeddingBackfillOption>().BindConfiguration(nameof(EmbeddingBackfillOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<EmbeddingBackfillOption>>().Value);

// embedding üretici — düz deterministik API çağrısı ("agent" davranışı değil; ayrı worker yok).
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var openAi = sp.GetRequiredService<OpenAiOption>();
    return new OpenAI.OpenAIClient(openAi.ApiKey)
        .GetEmbeddingClient(openAi.EmbeddingModel)
        .AsIEmbeddingGenerator();
});

// geçmiş katalog backfill'i — her açılışta idempotent tarama (FR-008); iş yoksa no-op.
builder.Services.AddHostedService<EmbeddingBackfillService>();

// serbest-sorgu kapısı ayarları (RolePassword user-secrets'tan; fail-fast).
builder.Services.AddOptions<AgentQueryOption>().BindConfiguration(nameof(AgentQueryOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AgentQueryOption>>().Value);

// R1: view + kısıtlı rol bootstrap'ı — AddMarten SONRASI kayıt şart (mt_doc tabloları önce kurulur).
builder.Services.AddHostedService(sp => new Storefront.Api.AgentSql.AgentQuerySurfaceBootstrap(
    storefrontDb,
    sp.GetRequiredService<AgentQueryOption>(),
    sp.GetRequiredService<ILogger<Storefront.Api.AgentSql.AgentQuerySurfaceBootstrap>>()));

// R2: kısıtlı bağlantı — storefrontDb conn-string'i rol kimliğiyle; TEK yetki view SELECT'i.
builder.Services.AddSingleton(sp =>
{
    var opt = sp.GetRequiredService<AgentQueryOption>();
    var csb = new Npgsql.NpgsqlConnectionStringBuilder(storefrontDb)
    {
        Username = opt.RoleName,
        Password = opt.RolePassword
    };
    return new Storefront.Api.AgentSql.AgentQueryConnectionSource(
        Npgsql.NpgsqlDataSource.Create(csb.ConnectionString));
});

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("storefront");

builder.Services.AddHttpContextAccessor();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp");

await app.RunAsync();