using Common.Auths;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ScoutAgent.Api.Extensions;

// 007: ScoutAgent kimlik DOĞRULAR ama audience doğrulamaz. Gerekçe: agent kullanıcının KENDİ token'ını
// (SPA'ya basılmış) okur — sub'dan sohbet sahipliği kurar; audience servis-başına (library.api…) olduğu
// için SPA token'ında scout.api yok. Audience + scope zorlaması DOWNSTREAM BC'lerde (kişisel /mcp uçları).
// Burada issuer + imza + ömür doğrulanır: IdP-basılı token güvenilir, sub güvenilir. Yeni scope/resource GEREKMEZ.
public static class AuthExtensions
{
    public static WebApplicationBuilder AddScoutAuth(this WebApplicationBuilder builder)
    {
        var identity = builder.Configuration.GetSection(nameof(IdentityOption)).Get<IdentityOption>()!;

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = identity.Address;
                options.RequireHttpsMetadata = false;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = false,
                    ValidateIssuer = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                };
            });

        builder.Services.AddAuthorization();
        return builder;
    }
}
