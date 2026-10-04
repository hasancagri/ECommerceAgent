var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcılık (Marten event-log + async projection + Wolverine entegrasyonu) → Extensions/MartenExtensions.cs.
builder.AddStorefrontMarten();

// Mesajlaşma (Wolverine + RabbitMQ topoloji + handler keşfi) → Extensions/MessagingExtensions.cs.
builder.AddStorefrontMessaging();

// 086: Elasticsearch arama projeksiyonu — client DI + açılış index bootstrap → Search/ElasticsearchExtensions.cs.
builder.AddStorefrontSearch();

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

// OpenAI embedding config — fail-fast (ApiKey yoksa açılmaz). Projeksiyon (açıklama embedding) + sorgu
// kapısı ({{EMBED}}) tüketir. Tüketici düz T enjekte eder.
builder.Services.AddOptions<OpenAiOption>().BindConfiguration("OpenAI")
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<OpenAiOption>>().Value);

// embedding üretici — düz deterministik API çağrısı ("agent" davranışı değil; ayrı worker yok).
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var openAi = sp.GetRequiredService<OpenAiOption>();
    return new OpenAI.OpenAIClient(openAi.ApiKey)
        .GetEmbeddingClient(openAi.EmbeddingModel)
        .AsIEmbeddingGenerator();
});

// 086: Redis + caching aspect SÖKÜLDÜ — cache'lenen slice yok (query_storefront agent sorgusu
// cache'lenmez; ES zaten hızlı). Eski read-model REST facet cache'inin kalıntısıydı.

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