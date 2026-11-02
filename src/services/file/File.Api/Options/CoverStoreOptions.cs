using System.ComponentModel.DataAnnotations;

namespace FileApi.Options;

public enum CoverStoreBackend
{
    Local,  // {RootPath}/covers/{isbn} (+ .ct sidecar) — yerel disk
    R2      // Cloudflare R2 (S3-uyumlu); content-type native metadata (sidecar yok)
}

// Kapak deposu yapılandırması. RootPath ZORUNLU — yerel serve VE local→R2 sync kaynağı için gerekli.
// Backend serve/store için hangi IFileStore impl'i seçileceğini belirler.
public class CoverStoreOptions
{
    public const string SectionName = "CoverStore";

    [Required]
    public string RootPath { get; set; } = default!;

    public CoverStoreBackend Backend { get; set; } = CoverStoreBackend.Local;
}
