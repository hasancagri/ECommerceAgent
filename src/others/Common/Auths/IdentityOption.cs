namespace Common.Auths;

// 005: Platform.Auth NuGet'inden Common'a katlandı (kimlik makamı çözüm içinde — ADR
// adr-identity-inward). Address = yerel Identity.Server issuer'ı (https://localhost:5001).
public class IdentityOption
{
    public required string Address { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
}
