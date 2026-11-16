var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// 012 app-BFF: istemci-yüzü aramanın tek giriş noktası (web + ileride mobile). Durumsuz orkestratör —
// DB YOK, domain aggregate YOK. Catalog'un public REST aramasını çağırır + login aramada UserSearched yayınlar.

// JWT OPSİYONEL (007 deseni): login kullanıcıyı token'dan çöz; anonim serbest (endpoint RequireAuthorization
// YOK → anonim geçer). ValidateAudience=false — SPA token audience per-BC değil (yoksa login reddedilip
// anonim sanılır, capture kopar). Issuer/imza/süre doğrulanır.
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

// Wolverine: UserSearched YALNIZ publish (fanout). DB yok → outbox yok (durumsuz; publish RabbitMQ-durable).
builder.Host.UseWolverine(opts =>
{
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();
    rabbit.DeclareExchange(RabbitMqConstants.UserSearched.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
    });
    opts.PublishMessage<IntegrationEvents.UserSearched>()
        .ToRabbitExchange(RabbitMqConstants.UserSearched.Exchange);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ICurrentUser, CurrentUser>();
// Catalog public REST istemcisi (Aspire service discovery → http://catalog-api).
builder.Services.AddHttpClient("catalog", c => c.BaseAddress = new Uri("http://catalog-api"));

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

// Keşif arama yüzeyi: POST /api/discovery/search (gateway /api/discovery/** → bff).
app.MapDiscoverySearch();

await app.RunAsync();
