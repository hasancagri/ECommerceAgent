using Microsoft.Extensions.Options;
using ScoutAgent.Api;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcılık (Marten scoutDb + Conversation alias + Wolverine entegrasyonu).
builder.AddScoutMarten();

// Mesajlaşma (Wolverine süreç-içi bus — command/query handler keşfi; RabbitMQ yok).
builder.AddScoutMessaging();

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();
// MCP client HttpClient'ları (service discovery ServiceDefaults'ta HttpClient'lara ekli).
// "mcp-anon" = anonim uç (katalog); "mcp-authed" = kişisel uçlar + kullanıcı token'ını taşıyan handler.
builder.Services.AddTransient<ScoutAgent.Api.Agents.UserTokenForwardingHandler>();
builder.Services.AddHttpClient("mcp-anon");
builder.Services.AddHttpClient("mcp-authed")
    .AddHttpMessageHandler<ScoutAgent.Api.Agents.UserTokenForwardingHandler>();

// Tip'li Options (IConfiguration'dan doğrudan okuma yok).
builder.Services.AddOptions<ScoutOptions>()
    .BindConfiguration(nameof(ScoutOptions))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ScoutOptions>>().Value);

builder.Services.AddOptions<McpEndpointsOptions>()
    .BindConfiguration(nameof(McpEndpointsOptions))
    .ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<McpEndpointsOptions>>().Value);

// 007 US4: kimlik doğrulama (sub → sohbet sahipliği). Audience doğrulanmaz — bkz. AuthExtensions.
builder.AddScoutAuth();

// 007 Step 2 (sıradaki): MAF agent (Ollama IChatClient) + 4 MCP client + chat endpoint.
// Bu iskelet yalnız servisi ayağa kaldırır (Marten + Wolverine + health).

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

// 007: Kâşif sohbet yüzeyi (gateway /api/chat/** → buraya).
app.MapChatEndpoints();

await app.RunAsync();
