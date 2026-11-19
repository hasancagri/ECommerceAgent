using System.ClientModel;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using ScoutAgent.Api;
using ScoutAgent.Api.Domains.Conversations;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcılık (Marten scoutDb + Conversation alias + Wolverine entegrasyonu).
builder.AddScoutMarten();

// Mesajlaşma (Wolverine süreç-içi bus — command/query handler keşfi; RabbitMQ yok).
builder.AddScoutMessaging();

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();
// Tek MCP client HttpClient'ı (service discovery ServiceDefaults'ta ekli). İsteğin kendi bearer'ını
// downstream /mcp çağrısına taşır (UserTokenForwardingHandler); token yoksa header düşer → kişisel uç 401.
builder.Services.AddTransient<ScoutAgent.Api.Agents.UserTokenForwardingHandler>();
builder.Services.AddHttpClient(ScoutAgent.Api.Agents.McpToolSource.HttpClientName)
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

// OpenAI ChatClient → IChatClient (gpt-4o-mini). UseFunctionInvocation ŞART: modelin tool_call'ını
// OTOMATİK yürütür (MCP tool'u çağır, sonucu modele geri besle, nihai cevap). Onsuz tool çalışmaz.
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var scout = sp.GetRequiredService<ScoutOptions>();
    IChatClient client = new OpenAIClient(new ApiKeyCredential(scout.ApiKey))
        .GetChatClient(scout.Model)
        .AsIChatClient();
    return client.AsBuilder().UseFunctionInvocation().Build();
});

// 020-A: burst rate limit (dakika-pencere, in-memory). Partition: login sub / anonim IP; limit auth'a
// göre. Aşınca 429 (B ile aynı gövde). UseRateLimiter auth'tan SONRA → partition kullanıcıyı bilir.
builder.Services.AddRateLimiter(limiter =>
{
    limiter.AddPolicy("chat", context =>
    {
        var opts = context.RequestServices.GetRequiredService<ScoutOptions>();
        var currentUser = context.RequestServices.GetRequiredService<ICurrentUser>();
        currentUser.Load(context.User);
        Guid? userId = currentUser.Id == Guid.Empty ? null : currentUser.Id;
        var key = ScoutUsageKey.Partition(userId, context.Connection.RemoteIpAddress?.ToString());
        var permit = userId is null ? opts.AnonPerMinute : opts.UserPerMinute;
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permit,
            Window = TimeSpan.FromMinutes(1),
            // false: pencere acquire-anında kontrol edilir (arka-plan timer YOK) → boşta kalan partition
            // yerleşik idle-cleanup'la geri alınır. true olsaydı her IP kalıcı timer+limiter → bellek sızar.
            AutoReplenishment = false,
        });
    });
    limiter.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.HttpContext.Response.WriteAsJsonAsync(new { message = ScoutResourceConstants.TOO_MANY_REQUESTS }, ct);
    };
});

// 007 US4: kimlik doğrulama (sub → sohbet sahipliği). Audience doğrulanmaz — bkz. AuthExtensions.
builder.AddScoutAuth();

// 007 Step 2 (sıradaki): MAF agent (Ollama IChatClient) + 4 MCP client + chat endpoint.
// Bu iskelet yalnız servisi ayağa kaldırır (Marten + Wolverine + health).

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

// 020-A: auth'tan SONRA (partition login kullanıcıyı çözebilsin).
app.UseRateLimiter();

// 007: Kâşif sohbet yüzeyi (gateway /api/chat/** → buraya).
app.MapConversationEndpoints();

await app.RunAsync();
