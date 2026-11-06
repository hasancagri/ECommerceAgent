var builder = WebApplication.CreateBuilder(args);

// 084: uygulama-kayıt sözleşmesini bağla + DOĞRULA + projekte et (boot'ta bir kez). Ad-uzayı
// çakışması / geçersiz alt-küme → açılış REDDEDER (SC-004). Bootstrap okuma (DI henüz yok) — Config'in
// service-discovery/connString istisnasıyla aynı sınıf. Options ValidateOnStart ikinci güvence.
var appRegistry = AppRegistry.Build(
    builder.Configuration.GetSection("AppRegistry").Get<AppRegistryOptions>() ?? new AppRegistryOptions());
AppRegistry.Initialize(appRegistry);
builder.Services.AddSingleton(appRegistry);
builder.Services.AddOptions<AppRegistryOptions>()
    .BindConfiguration("AppRegistry")
    .Validate(o => AppRegistryValidator.Validate(o).Count == 0, "AppRegistry doğrulaması başarısız (ad-uzayı çakışması / geçersiz scope).")
    .ValidateOnStart();

// 084: issuer artık config'ten (dağıtım-nötr; "localhost:5001" varsayımı kaldırıldı). HTTPS zorunlu.
var issuer = builder.Configuration["OpenIddict:Issuer"] ?? "https://localhost:5001";
if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri) || issuerUri.Scheme != Uri.UriSchemeHttps)
    throw new InvalidOperationException($"OpenIddict:Issuer geçerli bir HTTPS URL olmalı: '{issuer}'.");

// 030 RBAC: /Admin/* yönetim sayfaları cookie kullanıcısının admin rolünü ister (D3).
builder.Services.AddRazorPages(options =>
    options.Conventions.AuthorizeFolder("/Admin", "AdminRole"));

// Aspire çalışma anında enjekte eder; design-time (migration üretimi) için fallback.
var connectionString = builder.Configuration.GetConnectionString("identityDb")
                       ?? "Host=localhost;Port=5432;Database=identityDb;Username=postgres;Password=postgres";

var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
    // OpenIddict EF Core store'ları aynı context'i kullanır.
    options.UseOpenIddict();
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// House-style Options: appsettings section'lari tip'li POCO'ya bagla (config[...] magic-string yasak).
builder.Services.AddOptions<Identity.Server.Options.BootstrapAdmin>().BindConfiguration(nameof(Identity.Server.Options.BootstrapAdmin))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Identity.Server.Options.BootstrapAdmin>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Identity.Server.Options.BootstrapAdmin>>().Value);

// 030 RBAC: token verme yolunda rol→scope demeti + admin yönetim servisi.
builder.Services.AddScoped<Identity.Server.Rbac.RoleScopeQuery>();
builder.Services.AddScoped<Identity.Server.Rbac.RoleAssignmentService>();

// Login/challenge yolları + "beni hatırla" süresi (persistent cookie bu kadar yaşar).
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = Identity.Server.Pages.Login.LoginOptions.RememberMeLoginDuration;
});

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore().UseDbContext<ApplicationDbContext>();
        // 005: AdminAgentApplicationManager (loopback muafiyeti) KIRPILDI — dış-agent istemcisi yok.
    })
    .AddServer(options =>
    {
        // 084: issuer config'ten (yukarıda doğrulandı, HTTPS zorunlu). Servislerin Authority değeri buna bağlı.
        options.SetIssuer(issuerUri);

        options.SetAuthorizationEndpointUris("connect/authorize")
               .SetTokenEndpointUris("connect/token")
               .SetUserInfoEndpointUris("connect/userinfo")
               .SetEndSessionEndpointUris("connect/logout")
               // 061: dış agent bağlantı koparma (FR-009) — OpenIddict kendi işler, passthrough yok.
               .SetRevocationEndpointUris("connect/revocation");

        options.AllowAuthorizationCodeFlow()
               .AllowClientCredentialsFlow()
               .AllowRefreshTokenFlow();

        options.RegisterScopes([.. Config.AllApiScopes, .. Config.IdentityScopes]);

        // 085 R3: istemci-tavanı zorlaması OpenIddict'in scope-izin ÖN-validasyonundan (izinsiz
        // talep = invalid_scope RED) ScopeResolver kesişimine taşınır (izinsiz talep sessizce elenir).
        // Union PRM'de müşteri istemcisi admin scope'unu da talep edebilir — bu olmadan bağlantı kırılırdı
        // (FR-006). Tavan KALKMAZ, yalnız YER değiştirir: bkz. ClientCeilingResolver + ScopeResolver.Resolve.
        options.IgnoreScopePermissions();

        // WebApp "Sign Up" akışı prompt=create gönderir; kayıtlı olmasa 400 ile reddedilirdi.
        options.RegisterPromptValues(PromptValues.Create);

        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        // Access token düz imzalı JWT olsun ki servislerin JwtBearer'ı çözebilsin.
        options.DisableAccessTokenEncryption();

        // R3: access token scope claim'ini çoklu değere çevir (Duende paritesi).
        options.AddEventHandler(ScopeClaimArrayHandler.Descriptor);

        // 005: DCR discovery metadata'sı KIRPILDI (registration_endpoint ilan edilmez).

        // 061 (R5): RFC 8707 resource parametresi yok sayılır — audience scope eşlemesinden.
        options.AddEventHandler(IgnoreResourceParameterHandler.ForAuthorization.Descriptor);
        options.AddEventHandler(IgnoreResourceParameterHandler.ForToken.Descriptor);

        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .EnableUserInfoEndpointPassthrough()
               .EnableEndSessionEndpointPassthrough()
               .EnableStatusCodePagesIntegration();
    });

// Açılışta idempotent client + scope seed.
builder.Services.AddHostedService<SeedHostedService>();

// 005: ApiKey JWT bearer + apikeys.manage + agent-authenticated policy'leri KIRPILDI
// (ApiKey uçları ve dış-agent yüzeyi yok). Kalan tek policy: admin UI guard'ı.
builder.Services.AddAuthorization(options =>
{
    // 030 RBAC: IdP admin UI guard'ı — Identity cookie principal'ında admin rolü (D3).
    // İlke V istisnası: IdP kendi iç yüzeyini rolle korur; downstream yalnız scope.
    options.AddPolicy("AdminRole", policy =>
        policy.RequireRole(Identity.Server.Rbac.RoleAssignmentService.AdminRole));
});

var app = builder.Build();

// Açılışta migration'ları uygula (dev kolaylığı; Postgres Aspire ile hazır olur).
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
}

// Reverse-proxy (traefik/ingress) TLS'i sonlandırır → backend http görür. X-Forwarded-Proto/For'u GÜVEN
// → app https'i doğru algılar (cookie Secure/SameSite=None, OIDC redirect URL, antiforgery). KnownProxies/
// Networks TEMİZLENİR: k8s'te proxy IP'si dinamik (loopback değil), aksi halde header'lar yok sayılır.
var forwardedOptions = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// OIDC uçları (OpenIddict passthrough ile ASP.NET Core'da işlenir).
app.MapAuthorizeEndpoint();
app.MapTokenEndpoint();
app.MapUserInfoEndpoint();
app.MapLogoutEndpoint();
// 005: DCR (/connect/register) + ApiKey uçları KIRPILDI — istek 404.

app.MapRazorPages().RequireAuthorization();

app.Run();