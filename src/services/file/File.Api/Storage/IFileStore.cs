namespace FileApi.Storage;

// FR-008 backend-bağımsız depolama kontratı. Bugün LocalDiskFileStore; sonraki spec
// S3FileStore/MinioFileStore aynı arayüzü implemente eder → serve + migration değişmez.
public interface IFileStore
{
    // İçerik var mı (idempotent migration skip temeli).
    Task<bool> ExistsAsync(string key, CancellationToken ct);

    // İçerik + content-type yazar (üzerine yazar — ürün başına tek kapak).
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);

    // (stream, content-type) döner; yoksa null.
    Task<(Stream Content, string ContentType)?> TryGetAsync(string key, CancellationToken ct);
}
