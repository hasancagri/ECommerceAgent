namespace Payment.Api.Options;

// hosted-CF ödeme yapılandırması. IConfiguration'dan doğrudan okuma YASAK → tip'li POCO
// (BindConfiguration + ValidateOnStart). Tüketici düz PaymentOptions enjekte eder (IOptions değil).
public class PaymentOptions
{
    // Ödeme girişimi terk-timeout (saniye). Aşılırsa Pending intent Expire edilir (FR-011).
    public int IntentTimeoutSeconds { get; set; } = 300;

    // PG → store callback HMAC-SHA256 gizli anahtarı. MerchantKey'den AYRI (FR-006, sızıntı yalıtımı).
    [Required] public string CallbackSecret { get; set; } = string.Empty;

    // Dış PaymentGateway (DropShop) hosted-payment taban adresi.
    [Required] public string PgBaseUrl { get; set; } = string.Empty;
}