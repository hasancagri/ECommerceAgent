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

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

// 013: arama geçmişi yüzeyi (gateway /api/personalization/** → bu servis). Hepsi auth-zorunlu, kendi-veri.
app.MapSearchHistoryEndpoints();

await app.RunAsync();
