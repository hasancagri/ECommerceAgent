var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcılık (Marten + şema/index + Wolverine entegrasyonu) → Extensions/MartenExtensions.cs.
builder.AddLibraryMarten();

// Mesajlaşma (Wolverine + RabbitMQ topoloji + handler keşfi) → Extensions/MessagingExtensions.cs.
builder.AddLibraryMessaging();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.LibraryRead,
    AuthorizationScopes.LibraryWrite,
    AuthorizationScopes.OpsDeadletter);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();

// 014: query-only MCP ucu GERİ (ADR adr-mcp-inward-agent — first-party agent-içi MCP, İlke I meşru
// kanal; dış/BYO yüzey kapalı). YALNIZ ScoutAgent tüketir; kullanıcı kitaplığı (get_my_library).
// Tool'lar assembly taramasıyla bulunur (Features/Agents/Queries/*.cs [McpServerToolType]).
builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

// 014: kişisel-veri MCP ucu — scope-korumalı (library.read; kullanıcı KENDİ kitaplığı). Anonim 401.
app.MapMcp("/mcp").RequireAuthorization(AuthorizationScopes.LibraryRead);

await app.RunAsync();