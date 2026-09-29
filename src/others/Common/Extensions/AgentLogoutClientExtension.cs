namespace Common.Extensions;

// logout: korumalı servislerin `logout` MCP tool'u, kullanıcının Bearer'ını Identity.Server'ın
// agent-logout ucuna forward eder. Named client tek yerde (BaseAddress = IdentityOption.Address,
// dev self-signed cert bypass) — her servis tek satırla açar, tool relative path POST'lar.
public static class AgentLogoutClientExtension
{
    public const string HttpClientName = "agent-logout";

    public static IServiceCollection AddAgentLogoutClient(this IServiceCollection services,
        IConfiguration configuration)
    {
        var identityOptions = configuration.GetSection(nameof(IdentityOption)).Get<IdentityOption>()!;

        services.AddHttpClient(HttpClientName, client => client.BaseAddress = new Uri(identityOptions.Address))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                // Dev: Identity.Server self-signed sertifikasına iç-servis çağrısı (ApiKey deseniyle aynı).
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            });

        return services;
    }
}