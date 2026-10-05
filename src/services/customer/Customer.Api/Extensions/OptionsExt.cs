using Microsoft.Extensions.Options;

namespace Customer.Api.Extensions;

public static class OptionsExt
{
    public static IServiceCollection AddOptionsExt(this IServiceCollection services)
    {
        // DropShopVault (kart-saklama) config söküldü. Yalnız onboarding kaldı.
        // DropShop onboarding (PG Merchant.Api MCP + Identity) — section "DropShopOnboarding".
        // Alanlar opsiyonel: config yoksa tool'lar dostane "yapılamıyor" döner (IsConfigured).
        services.AddOptions<Customer.Api.Onboarding.DropShopOnboardingOption>()
            .BindConfiguration("DropShopOnboarding")
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<Customer.Api.Onboarding.DropShopOnboardingOption>(sp =>
            sp.GetRequiredService<IOptions<Customer.Api.Onboarding.DropShopOnboardingOption>>().Value);

        // 087: first-party merchant iş/finansal künyesi — register gövdesinde S2S gider (sunucu-tarafı okunur,
        // MCP/sohbete girmez). Section "MerchantBusinessProfile".
        services.AddOptions<Customer.Api.Onboarding.MerchantBusinessProfileOption>()
            .BindConfiguration("MerchantBusinessProfile")
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<Customer.Api.Onboarding.MerchantBusinessProfileOption>(sp =>
            sp.GetRequiredService<IOptions<Customer.Api.Onboarding.MerchantBusinessProfileOption>>().Value);

        // 087: CredentialEntryOptions SÖKÜLDÜ (elle-giriş ekranı emekli; credential HMAC-callback'le gelir).
        return services;
    }
}
