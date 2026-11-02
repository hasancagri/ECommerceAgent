var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcılık (Marten + şema/index + Wolverine entegrasyonu) → Extensions/MartenExtensions.cs.
builder.AddDiscountMarten();

// Mesajlaşma (Wolverine + RabbitMQ topoloji) → Extensions/MessagingExtensions.cs.
builder.AddDiscountMessaging();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.DiscountWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();

// Dış yüzey ŞU AN YOK (003 FR-007) — kampanya tanımlama REST admin'le sonraki dilimde açılır.
// Yürürlük taraması (CampaignWindowProcess) SONRAKİ dilim — duyuru o gelince başlar.

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

await app.RunAsync();
