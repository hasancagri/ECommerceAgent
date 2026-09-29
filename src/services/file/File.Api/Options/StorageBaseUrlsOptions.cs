using FileApi.Domains.FileAsset;

namespace FileApi.Options;

// URL resolver yapılandırması (Desen B). Full URL veriye gömülmez; StorageFilePath (key) + o
// StorageType'ın base'i → URL (CoverUrlResolver). Base config'te → repoint tek yerden.
// Bir dosya birden çok fiziki depoda (R2/Local/…) yaşayabilir; DefaultStorageType = çözümlemede
// tercih edilen depo (o konum varsa onun URL'i döner; yoksa mevcut ilk konuma düşer).
public class StorageBaseUrlsOptions
{
    public const string SectionName = "StorageBaseUrls";

    // Anahtar = StorageType adı (Local/R2/S3/...); değer = public base URL (sonda / olsun/olmasın; resolver kırpar).
    public Dictionary<string, string> Bases { get; set; } = new();

    // Çözümlemede tercih edilen depo. Dosyada bu StorageType konumu varsa onun URL'i döner.
    public StorageType DefaultStorageType { get; set; } = StorageType.R2;
}