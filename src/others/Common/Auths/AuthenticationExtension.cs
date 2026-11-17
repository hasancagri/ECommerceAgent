using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Common.Auths;

// 005: Platform.Auth NuGet'inden Common'a katlandı — davranış birebir (JWT Bearer + scope policy'leri).
public static class AuthenticationExtension
{
    public static IServiceCollection AddAuthenticationAndAuthorizationExtension(this IServiceCollection services,
        IConfiguration configuration, params string[] scopes)
    {
        var identityOptions = configuration.GetSection(nameof(IdentityOption))
            .Get<IdentityOption>()!;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = identityOptions.Address;
                options.Audience = identityOptions.Audience;
                options.RequireHttpsMetadata = false;

                // Claim'leri token'daki haliyle birak (scope/role/email kisa adlariyla).
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // 014: SPA access-token audience TAŞIMAZ (yalnız scope) → audience doğrulaması
                    // SPA token'ını reddediyordu (401). Tek-IdP'de issuer+imza+süre yeterli güvence;
                    // audience düşük-değer. Catalog'un 012 inline-hack'inin de yerini alır.
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidateIssuer = true,
                };

                options.AutomaticRefreshInterval = TimeSpan.FromHours(24);
                options.RefreshInterval = TimeSpan.FromSeconds(30);
            });

        services.AddAuthorization(options =>
        {
            // Her scope icin policy: gecerli (authenticated) token + ilgili "scope" claim'i.
            foreach (var scope in scopes)
                options.AddPolicy(scope, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("scope", scope);
                });
        });

        return services;
    }
}
