namespace Identity.Server.Connect;

// 061 (R5): MCP istemcileri (Claude Code / mcp-remote) RFC 8707 `resource` parametresiyle
// MCP URL'i gönderir (http://localhost:<gw>/mcp/<servis>). OpenIddict bu değeri scope'lara
// bağlı resource'ların (basket.api gibi mantıksal audience adları) alt kümesi olarak doğrular
// → invalid_target (ID2190). Dilim kararı: audience mevcut scope→resource eşlemesinden üretilir,
// `resource` parametresi YOK SAYILIR — token yine yalnız ilgili servisin audience'ını taşır.
public static class IgnoreResourceParameterHandler
{
    public sealed class ForAuthorization
        : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateAuthorizationRequestContext>
    {
        public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
            OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateAuthorizationRequestContext>()
                .UseSingletonHandler<ForAuthorization>()
                .SetOrder(int.MinValue + 100_000)
                .SetType(OpenIddictServerHandlerType.Custom)
                .Build();

        public ValueTask HandleAsync(OpenIddictServerEvents.ValidateAuthorizationRequestContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            context.Request.Resources = null;
            return default;
        }
    }

    public sealed class ForToken
        : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenRequestContext>
    {
        public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
            OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenRequestContext>()
                .UseSingletonHandler<ForToken>()
                .SetOrder(int.MinValue + 100_000)
                .SetType(OpenIddictServerHandlerType.Custom)
                .Build();

        public ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenRequestContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            context.Request.Resources = null;
            return default;
        }
    }
}
