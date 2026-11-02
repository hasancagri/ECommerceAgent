var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcılık (Marten + şema/index + Wolverine entegrasyonu) → Extensions/MartenExtensions.cs.
builder.AddStockMarten();

// Mesajlaşma (Wolverine + RabbitMQ topoloji + handler keşfi) → Extensions/MessagingExtensions.cs.
builder.AddStockMessaging();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.StockWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();

// Dış yüzey ŞU AN YOK (002 FR-006) — stok girişi/okuma REST first-party ile sonraki dilimde açılır.

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

await app.RunAsync();
