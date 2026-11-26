var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// KalÄ±cÄ±lÄ±k (Marten + Åema/index + Wolverine entegrasyonu) â Extensions/MartenExtensions.cs.
builder.AddLibraryMarten();

// MesajlaÅma (Wolverine + RabbitMQ topoloji + handler keÅfi) â Extensions/MessagingExtensions.cs.
builder.AddLibraryMessaging();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// Son Gezdiğim Kitaplar (2026-11-26): REST yüzeyi SPA access-token'ıyla çağrılır (audience taşımaz) →
// Common.AddAuthenticationAndAuthorizationExtension (ValidateAudience=true) reddeder. Personalization 013
// deseni: inline JWT, ValidateAudience=false (issuer/imza/süre doğrulanır). Named scope YOK — authenticated
// + kendi userId'si (sub) yeter (kişisel-veri). /mcp (ScoutAgent get_my_library) de bu şemayla auth olur.
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
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ICurrentUser, CurrentUser>();

// 014: query-only MCP ucu GERÄ° (ADR adr-mcp-inward-agent â first-party agent-iÃ§i MCP, Ä°lke I meÅru
// kanal; dÄ±Å/BYO yÃ¼zey kapalÄ±). YALNIZ ScoutAgent tÃ¼ketir; kullanÄ±cÄ± kitaplÄ±ÄÄ± (get_my_library).
// Tool'lar assembly taramasÄ±yla bulunur (Features/Agents/Queries/*.cs [McpServerToolType]).
builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

// Son Gezdiğim Kitaplar (2026-11-26): library ilk REST yüzeyi (gateway /api/library/** → bu servis).
app.MapUserBookViewEndpoints();

// 014: kiÅisel-veri MCP ucu â scope-korumalÄ± (library.read; kullanÄ±cÄ± KENDÄ° kitaplÄ±ÄÄ±). Anonim 401.
app.MapMcp("/mcp").RequireAuthorization();  // 014: personal data, no named scope (013 pattern) - identity from sub

await app.RunAsync();