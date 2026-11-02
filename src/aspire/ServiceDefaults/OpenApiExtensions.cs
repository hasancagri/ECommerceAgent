using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;

namespace Microsoft.Extensions.Hosting;

// Servislere yerlesik OpenAPI belgesi ve Scalar arayuzu ekler.
// Paketler ServiceDefaults.csproj'da; ServiceDefaults'u referans eden her servis kullanabilir.
public static class OpenApiExtensions
{
    // OpenAPI belge uretimini kaydeder. Auth guvenlik semasi eklenmez (sade dokumantasyon).
    public static IHostApplicationBuilder AddOpenApiDocumentation(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
        return builder;
    }

    // OpenAPI JSON (/openapi/v1.json) ve Scalar UI (/scalar/v1) yalnizca Development'ta acilir.
    // Production'da hicbir dokumantasyon endpoint'i map'lenmez.
    public static WebApplication MapScalarDocumentation(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        return app;
    }
}