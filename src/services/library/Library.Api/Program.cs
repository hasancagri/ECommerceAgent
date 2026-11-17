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

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.LibraryRead,
    AuthorizationScopes.LibraryWrite,
    AuthorizationScopes.OpsDeadletter);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();

// 014: query-only MCP ucu GERÄ° (ADR adr-mcp-inward-agent â first-party agent-iÃ§i MCP, Ä°lke I meÅru
// kanal; dÄ±Å/BYO yÃ¼zey kapalÄ±). YALNIZ ScoutAgent tÃ¼ketir; kullanÄ±cÄ± kitaplÄ±ÄÄ± (get_my_library).
// Tool'lar assembly taramasÄ±yla bulunur (Features/Agents/Queries/*.cs [McpServerToolType]).
builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

// 014: kiÅisel-veri MCP ucu â scope-korumalÄ± (library.read; kullanÄ±cÄ± KENDÄ° kitaplÄ±ÄÄ±). Anonim 401.
app.MapMcp("/mcp").RequireAuthorization();  // 014: personal data, no named scope (013 pattern) - identity from sub

await app.RunAsync();