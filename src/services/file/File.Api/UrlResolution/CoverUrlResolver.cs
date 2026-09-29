namespace FileApi.UrlResolution;

// Saf URL çözümleyici. (StorageType, StorageFilePath) + config-base → public URL. Full URL
// veriye gömülmez → provider/base değişince tek yerden (config) repoint. Dış-depoya çağrı YOK (SC-001).
// Bilinmeyen StorageType / eksik base → null (guard; çözümlenemez konum).
// Concrete tiple inject edilir (handler'lar) → Program'da açık AddSingleton (Scrutor AsImplementedInterfaces
// concrete kaydetmez; CallbackSignatureValidator emsali).
public sealed class CoverUrlResolver
{
    private readonly StorageBaseUrlsOptions _options;

    public CoverUrlResolver(StorageBaseUrlsOptions options) => _options = options;

    // Tercih edilen depo (config DefaultStorageType).
    public StorageType DefaultStorageType => _options.DefaultStorageType;

    // StorageType'a göre base URL bul + path'i birleştir. Base yoksa/boşsa → null.
    public string? Resolve(StorageType storageType, string storageFilePath)
    {
        if (string.IsNullOrWhiteSpace(storageFilePath))
            return null;

        if (!_options.Bases.TryGetValue(storageType.ToString(), out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            return null;

        return $"{baseUrl.TrimEnd('/')}/{storageFilePath.TrimStart('/')}";
    }
}