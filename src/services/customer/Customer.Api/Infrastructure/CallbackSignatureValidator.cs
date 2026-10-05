using System.Security.Cryptography;
using System.Text;
using Customer.Api.Onboarding;

namespace Customer.Api.Infrastructure;

// 087 US1: PG→store credential callback köken+bütünlük doğrulaması (077 Payment.Api aynası).
// X-Signature = HMAC-SHA256(CallbackSecret, raw_body) hex; store aynı hesabı yapar, sabit-zamanlı
// karşılaştırır. CallbackSecret BootstrapRegistrationKey ve MerchantKey'den AYRI (FR-004).
// Geçersiz/eksik → uç 400, ReceiveMerchantCredentials ÇAĞRILMAZ. Gövde imza öncesi deserialize EDİLMEZ.
public sealed class CallbackSignatureValidator(DropShopOnboardingOption options) : ITransientDependency
{
    public bool IsValid(string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(options.CallbackSecret))
            return false;

        var key = Encoding.UTF8.GetBytes(options.CallbackSecret);
        var computed = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(rawBody));
        var expectedHex = Convert.ToHexString(computed); // upper-case hex

        // hex karşılaştırma büyük/küçük harf duyarsız; sabit-zamanlı byte eşitliği.
        byte[] providedBytes;
        try
        {
            providedBytes = Convert.FromHexString(signatureHeader.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedBytes = Convert.FromHexString(expectedHex);
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}