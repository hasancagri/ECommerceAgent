using Common.Auths;

namespace Common.Extensions;

// Servisler (gateway HARİÇ) bunu AddAuthenticationAndAuthorizationExtension'dan SONRA çağırır.
// JWT bearer'a dokunmaz; ApiKey şemasını + resolve HttpClient'ını ekler. Default şema Bearer kalır;
// ApiKey EAGER olarak middleware ile çözülür (bkz. ApiKeyAuthenticationMiddleware).
public static class ApiKeyAuthenticationExtension
{
    public static IServiceCollection AddApiKeyAuthentication(this IServiceCollection services,
        IConfiguration configuration)
    {
        var identityOptions = configuration.GetSection(nameof(IdentityOption)).Get<IdentityOption>()!;
        var internalSecret = configuration["ApiKeyAuth:InternalSecret"];

        services.AddHttpClient(ApiKeyAuthenticationDefaults.HttpClientName, client =>
            {
                client.BaseAddress = new Uri(identityOptions.Address);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                // Dev: Identity.Server self-signed sertifikasına iç-servis çağrısı (deferred-auth duruşuyla uyumlu).
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            });

        services.AddAuthentication()
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.Scheme,
                options => options.InternalSecret = internalSecret);

        return services;
    }

    // UseAuthentication() ile UseAuthorization() arasına yerleştirilir.
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder app)
        => app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
}