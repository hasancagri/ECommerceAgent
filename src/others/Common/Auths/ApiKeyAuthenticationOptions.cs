namespace Common.Auths;

public static class ApiKeyAuthenticationDefaults
{
    public const string Scheme = "ApiKey";
    public const string HttpClientName = "apikey-resolve";
}

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    // Dış tüketicinin taşıdığı opak anahtar header'ı.
    public const string HeaderName = "X-User-Key";

    // Identity.Server'daki iç çözümleme uç noktası (base adres HttpClient'ta ayarlı).
    public string ResolvePath { get; set; } = "/api/keys/resolve";

    // Resolve/admin uçlarını koruyan v1 paylaşılan gizli değer (D8; sertleştirme ayrı iş).
    public string? InternalSecret { get; set; }
}