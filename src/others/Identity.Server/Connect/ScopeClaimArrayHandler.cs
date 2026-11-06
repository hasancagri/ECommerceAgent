namespace Identity.Server.Connect;

// R3 (EN KRİTİK): Duende paritesi. OpenIddict access token'a scope'u RFC 9068 gereği tek
// boşluk-ayrık string yazar; servisler ise RequireClaim("scope", x) / HasClaim("scope", x)
// ile TEK TEK değer arar → tek string'te sessizce 401/403 (fail-closed). JWT'de scope'u
// JSON DİZİSİ yaparsak JsonWebTokenHandler her elemanı ayrı "scope" claim'ine açar; böylece
// bugünkü çoklu-claim davranışı birebir korunur. Yalnız ACCESS TOKEN'a dokunur.
public sealed class ScopeClaimArrayHandler : IOpenIddictServerHandler<OpenIddictServerEvents.GenerateTokenContext>
{
    // AttachTokenPayload descriptor'ı doldurduktan SONRA, GenerateIdentityModelToken token'ı
    // üretmeden ÖNCE çalışsın diye onun bir önüne sıralanır.
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.GenerateTokenContext>()
            .UseSingletonHandler<ScopeClaimArrayHandler>()
            .SetOrder(OpenIddictServerHandlers.Protection.GenerateIdentityModelToken.Descriptor.Order - 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(OpenIddictServerEvents.GenerateTokenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Yalnız access token; id_token/refresh/authorization code dokunulmaz.
        // NOT: context.TokenType URN'dir (TokenTypeIdentifiers.AccessToken), kısa hint (TokenTypeHints) DEĞİL.
        if (context.TokenType is not TokenTypeIdentifiers.AccessToken)
            return default;

        var descriptor = context.SecurityTokenDescriptor;
        if (descriptor is not null &&
            descriptor.Claims.TryGetValue(Claims.Scope, out var value) &&
            value is string scope &&
            !string.IsNullOrEmpty(scope))
        {
            // Tek "a b c" string'i → ["a","b","c"]; JWT payload'ında JSON dizisi olur.
            descriptor.Claims[Claims.Scope] = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        return default;
    }
}

// access_token + (uygunsa) id_token claim destinasyonları. Duende'deki
// AlwaysIncludeUserClaimsInIdToken + ApiResource.UserClaims davranışının karşılığı.
public static class OidcClaimDestinations
{
    public static IEnumerable<string> GetDestinations(Claim claim) => claim.Type switch
    {
        // 030 RBAC: rol YALNIZ id_token'a gider (WebApp header link gibi UI kararı için). Access
        // token'a girmez → downstream servisler rolü hiç görmez, yalnız scope zorlar (İlke V).
        Claims.Role => [Destinations.IdentityToken],
        Claims.Subject or Claims.Name or Claims.Email =>
            [Destinations.AccessToken, Destinations.IdentityToken],
        _ => [Destinations.AccessToken],
    };
}