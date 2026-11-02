using System.ComponentModel.DataAnnotations;

namespace FileApi.Options;

// Cloudflare R2 (S3-uyumlu obje deposu) yapılandırması. Endpoint AccountId'den türetilir.
// Credential'lar (AccessKeyId/SecretAccessKey) user-secrets'ten gelir — config/repo'ya gömülmez.
// Yalnız Backend=R2 iken zorunlu (ValidateOnStart değil; Backend seçimine göre Program'da doğrulanır).
public class R2Options
{
    public const string SectionName = "R2";

    public string AccountId { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;

    // Müşteriye giden public taban URL (r2.dev ya da custom domain). Product.ImageUrl (Desen B) için;
    // depolamanın kendisi için gerekmez.
    public string PublicBaseUrl { get; set; } = string.Empty;

    // S3 endpoint: https://{AccountId}.r2.cloudflarestorage.com
    public string ServiceUrl => $"https://{AccountId}.r2.cloudflarestorage.com";
}