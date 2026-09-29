namespace Customer.Api.Onboarding;

// DropShop (PaymentGateway) Merchant.Api REST + Identity bağlantı config'i —
// section "DropShopOnboarding". Onboarding istemcisi (PgOnboardingClient) PG'ye MAKİNE kimliğiyle
// (client_credentials) gider; admin kullanıcı token'ı dış realm'e ASLA taşınmaz. ApiBaseUrl boşsa
// tool'lar "yapılamıyor" döner.
public class DropShopOnboardingOption
{
    public string IdentityAddress { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    // Merchant.Api onboarding yüzeyi merchant.write ister.
    public string Scope { get; set; } = "merchant.read merchant.write";

    public string TokenEndpoint => $"{IdentityAddress.TrimEnd('/')}/connect/token";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiBaseUrl);
}