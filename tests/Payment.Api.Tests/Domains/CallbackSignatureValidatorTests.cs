using System.Security.Cryptography;
using System.Text;

namespace Payment.Api.Tests.Domains;

// 094 US2 (İlke VIII): PG→EC webhook imza doğrulaması — saf HMAC birimi. Fail-closed: imza eşleşmez /
// sır boş / imza boş → false. Doğru imza (HMAC-SHA256 lowercase hex, ham gövde) → true.
public class CallbackSignatureValidatorTests
{
    private const string Secret = "dev-sim-callback-secret-0909";
    private const string Body = "{\"pgPaymentRef\":\"abc\",\"status\":\"Paid\",\"amount\":10.0}";

    private static string Sign(string secret, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    [Fact]
    public void Verify_CorrectSignature_ReturnsTrue()
    {
        var sig = Sign(Secret, Body);

        CallbackSignatureValidator.Verify(Secret, Body, sig).ShouldBeTrue();
    }

    [Fact]
    public void Verify_TamperedBody_ReturnsFalse()
    {
        var sig = Sign(Secret, Body);

        CallbackSignatureValidator.Verify(Secret, Body + " ", sig).ShouldBeFalse();
    }

    [Fact]
    public void Verify_WrongSecret_ReturnsFalse()
    {
        var sig = Sign("baska-sir", Body);

        CallbackSignatureValidator.Verify(Secret, Body, sig).ShouldBeFalse();
    }

    [Fact]
    public void Verify_EmptySignature_ReturnsFalse()
        => CallbackSignatureValidator.Verify(Secret, Body, "").ShouldBeFalse();

    [Fact]
    public void Verify_NullSignature_ReturnsFalse()
        => CallbackSignatureValidator.Verify(Secret, Body, null).ShouldBeFalse();

    [Fact]
    public void Verify_EmptySecret_ReturnsFalse()
        => CallbackSignatureValidator.Verify("", Body, Sign(Secret, Body)).ShouldBeFalse();
}
