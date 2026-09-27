using System.ComponentModel.DataAnnotations;

namespace Customer.Api.Options;

// 078 D1: hosted credential-giriş ekranı config'i — section "CredentialEntryOptions". Link tabanı
// HttpContext'ten ALINMAZ (MCP çağrısı platform MCP fasad proxy'sinden gelir; istek base'i Aspire iç
// adresidir, tarayıcıda çözülmez) → dışarıdan erişilir adres config'te (077 PG hosted-link emsali).
public class CredentialEntryOptions
{
    // Customer.Api'nin tarayıcıdan erişilir taban adresi (link: {PublicBaseUrl}/merchant-credentials/{token}).
    [Required] public string PublicBaseUrl { get; set; } = string.Empty;

    // Ekran linkinin ömrü (üretimden itibaren); tek kullanımlık token bu süre sonunda ölür.
    public TimeSpan LinkLifetime { get; set; } = TimeSpan.FromMinutes(60);
}