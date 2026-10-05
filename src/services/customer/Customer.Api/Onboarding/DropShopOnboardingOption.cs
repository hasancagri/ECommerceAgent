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

    // 087 FR-003: store→PG kayıt-ucu yetkisi (ortak bootstrap kayıt key'i; X-Registration-Key header).
    // m2m bearer taşıma kimliği; bootstrap key kayıt-ucu yetkisi — iki kaygı ayrı. Platform-seviyesi, rotate.
    public string BootstrapRegistrationKey { get; set; } = "";

    // 087 FR-004: PG→store asenkron credential callback'inin HMAC imzasını doğrulayan AYRI sır
    // (BootstrapRegistrationKey ve MerchantKey'den bağımsız; 077 CallbackSecret emsali).
    public string CallbackSecret { get; set; } = "";

    // 087 FR-004: store'un callback ucu için PG'ye bildirilen taban URL (dışarıdan erişilir adres;
    // istek base'i Aspire iç adresi olduğundan HttpContext'ten alınmaz — 077 hosted-link emsali).
    public string CallbackBaseUrl { get; set; } = "";

    public string TokenEndpoint => $"{IdentityAddress.TrimEnd('/')}/connect/token";

    // Kayıtta PG'ye bildirilen tam callback ucu (contracts/credential-callback.md).
    public string CallbackUrl => $"{CallbackBaseUrl.TrimEnd('/')}/internal/merchant-registration/callback";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiBaseUrl);
}