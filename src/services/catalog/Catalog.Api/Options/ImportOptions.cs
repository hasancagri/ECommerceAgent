using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.Options;

// 083 D6: Excel yükleme ekranı config'i — section "ImportOptions". Link tabanı HttpContext'ten ALINMAZ
// (import_catalog çağrısı platform MCP fasad proxy'sinden gelir; istek base'i Aspire iç adresidir, tarayıcıda
// çözülmez) → dışarıdan erişilir adres config'te (078 CredentialEntryOptions emsali).
public class ImportOptions
{
    // Catalog.Api'nin tarayıcıdan erişilir taban adresi (link: {PublicBaseUrl}/catalog-import/{token}).
    [Required] public string PublicBaseUrl { get; set; } = string.Empty;

    // Yükleme linkinin ömrü (üretimden itibaren); tek kullanımlık token bu süre sonunda ölür.
    public TimeSpan LinkLifetime { get; set; } = TimeSpan.FromMinutes(60);
}
