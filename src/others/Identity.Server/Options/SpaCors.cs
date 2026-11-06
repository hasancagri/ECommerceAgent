namespace Identity.Server.Options;

// 006: SPA origin'lerinin kapalı listesi — OIDC discovery/token/userinfo fetch'leri için CORS.
// Yeni SPA origin'i (prod adresi vb.) buraya eklenir; joker origin YASAK (token ucu açılmaz).
public static class SpaCors
{
    public const string PolicyName = "spa";

    public static readonly string[] AllowedOrigins = ["http://localhost:5173"];
}
