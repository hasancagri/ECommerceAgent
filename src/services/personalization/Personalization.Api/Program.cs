var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcılık (Marten + şema + Wolverine entegrasyonu) → Extensions/MartenExtensions.cs.
builder.AddPersonalizationMarten();

// Mesajlaşma (Wolverine + RabbitMQ: UserSearched tüketici) → Extensions/MessagingExtensions.cs.
builder.AddPersonalizationMessaging();

// 013: ilk HTTP yüzeyi (arama geçmişi). ZORUNLU JWT (anonim 401) — geçmiş kişiseldir. Inline
// (Catalog 012 deseni): SPA access-token audience taşımaz → Common.AddAuthenticationAndAuthorization
// (ValidateAudience=true) reddeder; o yüzden ValidateAudience=false. Issuer/imza/süre doğrulanır.
// Named scope YOK — authenticated + kendi userId'si (sub) yeterli (kişisel-veri erişimi, İlke V notu).
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
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ICurrentUser, CurrentUser>();

// 014: query-only MCP ucu (ADR adr-mcp-inward-agent — first-party ScoutAgent tüketir). get_search_history + 028 get_my_taste.
builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();

// 028: taste-vektör embedding config (ApiKey AppHost scout-openai-key env'inden). Tüketici düz T.
builder.Services.AddOptions<Personalization.Api.Options.PersonalizationEmbeddingOptions>()
    .BindConfiguration(nameof(Personalization.Api.Options.PersonalizationEmbeddingOptions))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Personalization.Api.Options.PersonalizationEmbeddingOptions>>().Value);

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

// 013: arama geçmişi yüzeyi (gateway /api/personalization/** → bu servis). Hepsi auth-zorunlu, kendi-veri.
app.MapSearchHistoryEndpoints();

// 014: kişisel MCP ucu — auth-zorunlu (named-scope YOK, kişisel-veri; 013 deseni). Yalnız ScoutAgent tüketir.
app.MapMcp("/mcp").RequireAuthorization();

await app.RunAsync();
