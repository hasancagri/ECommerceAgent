using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Extensions.Hosting;

// Sır kıyası TEK yerde: admin anahtarı (Gateway + Catalog çift kat) ve HMAC imza (Payment callback)
// hep buradan geçer. Sabit-zamanlı — erken dönen string eşitliği süreyle karakter sızdırır (timing).
public static class SecretCompare
{
    public static bool FixedTimeEquals(string provided, string expected)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
}