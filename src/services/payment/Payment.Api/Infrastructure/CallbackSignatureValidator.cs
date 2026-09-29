namespace Payment.Api.Infrastructure;

// US3: PG callback köken+bütünlük doğrulaması. X-Signature = HMAC-SHA256(CallbackSecret, raw_body)
// hex; store aynı hesabı yapar, sabit-zamanlı karşılaştırır. CallbackSecret MerchantKey'den AYRI (FR-006).
// Geçersiz/eksik → uç 401, HandlePaymentCallback ÇAĞRILMAZ. Sahte "ödendi" POST'unu keser.
public sealed class CallbackSignatureValidator(PaymentOptions options) : ITransientDependency
{
    public bool IsValid(string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        var key = Encoding.UTF8.GetBytes(options.CallbackSecret);
        var computed = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(rawBody));
        var expectedHex = Convert.ToHexString(computed); // upper-case hex

        var provided = signatureHeader.Trim();
        // hex karşılaştırma büyük/küçük harf duyarsız; sabit-zamanlı byte eşitliği.
        byte[] providedBytes;
        try
        {
            providedBytes = Convert.FromHexString(provided);
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedBytes = Convert.FromHexString(expectedHex);
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
