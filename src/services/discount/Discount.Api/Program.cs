using Discount.Api.Domains.ProductCatalogRefs;
using Discount.Api.Domains.ProductDiscounts;
using Discount.Api.Grpc;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcılık (Marten + şema/index + Wolverine entegrasyonu) → Extensions/MartenExtensions.cs.
builder.AddDiscountMarten();

// Mesajlaşma (Wolverine + RabbitMQ topoloji + handler keşfi) → Extensions/MessagingExtensions.cs.
builder.AddDiscountMessaging();

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.DiscountRead,
    AuthorizationScopes.AdminDiscountWrite);

// 085 R5: RFC 9728 keşfi (401 challenge + metadata) — /mcp slug'ında (admin scope'uyla). Anonim set YOK.
builder.Services.AddMcpResourceMetadata(builder.Configuration, "discount",
    AuthorizationScopes.AdminDiscountWrite);

builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();
builder.Services.AddGrpc();

// Tek MCP server, YALNIZ korumalı /mcp ucu (kampanya = admin işi, anonim set YOK). Tool-görünürlük
// budaması KALDIRILDI; yetki tek katman: handler'daki [RequiredScope(AdminDiscountWrite)].
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

// 079 US3: checkout S2S — Order.Api canlı indirim doğrulaması (discount.read).
app.MapGrpcService<Discount.Api.Grpc.Checkout.DiscountQueryGrpcService>().RequireAuthorization(AuthorizationScopes.DiscountRead);

// 085 R5: korumalı yönetim ucu — kimliksiz istek 401 + resource_metadata challenge. /mcp-admin öldü.
app.MapMcp("/mcp").RequireAuthorization();
app.MapMcpResourceMetadata();

await app.RunAsync();
