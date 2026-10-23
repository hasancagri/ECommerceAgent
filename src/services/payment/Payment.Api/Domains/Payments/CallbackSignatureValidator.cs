using System.Security.Cryptography;
using System.Text;

namespace Payment.Api.Domains.Payments;

// 094 (İlke VIII): PG→EC webhook imza doğrulaması. X-Signature = HMAC-SHA256(CallbackSecret, ham gövde)
// lowercase hex (PG StoreResultDelivery ile birebir). Sabit-zamanlı karşılaştırma (timing guard); sır
// loglanmaz. Ham gövde baytları üzerinden — JSON yeniden serialize edilmeden (PG raw_body'yi imzaladı).
public static class CallbackSignatureValidator
{
    public static bool Verify(string secret, string rawBody, string? signature)
    {
        if (string.IsNullOrEmpty(secret))
            return false; // sır yoksa fail-closed

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature ?? string.Empty));
    }
}
