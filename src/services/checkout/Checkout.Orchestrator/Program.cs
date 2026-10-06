using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Kalıcılık (Marten + Wolverine entegrasyonu) → Extensions/MartenExtensions.cs.
builder.AddCheckoutMarten();

// Mesajlaşma (Wolverine + RabbitMQ saga topoloji + handler keşfi) → Extensions/MessagingExtensions.cs.
builder.AddCheckoutMessaging();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.CheckoutWrite,
    AuthorizationScopes.OpsDeadletter);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

builder.Services.AddOptions<CheckoutOptions>().BindConfiguration(nameof(CheckoutOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<CheckoutOptions>(sp => sp.GetRequiredService<IOptions<CheckoutOptions>>().Value);

builder.Services.AddHttpContextAccessor();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    // 088: ölü-mesaj operatör tool'ları Common'da — açık kayıt.
    .WithTools<Common.Utils.DeadLetters.DeadLetterAdminMcpTools>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// POST /checkout REST giriş yüzü söküldü — checkout sağası yalnız broker StartCheckout ile doğar
// (Order.Api place_order yayınlar). REST endpoint YOK.

app.MapMcp("/mcp");

await app.RunAsync();