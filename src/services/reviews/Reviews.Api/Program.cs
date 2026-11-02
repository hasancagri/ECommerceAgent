var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcılık (Marten + şema/index + Wolverine entegrasyonu) → Extensions/MartenExtensions.cs.
builder.AddReviewsMarten();

// Mesajlaşma (Wolverine + RabbitMQ topoloji + handler keşfi) → Extensions/MessagingExtensions.cs.
builder.AddReviewsMessaging();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.ReviewsWrite,
    AuthorizationScopes.OpsDeadletter);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();

// MCP yüzeyi SÖKÜLDÜ (tertemiz sayfa) — /mcp yorum tool'ları + DLQ MCP kaldırıldı; REST first-party'ye.

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

await app.RunAsync();
