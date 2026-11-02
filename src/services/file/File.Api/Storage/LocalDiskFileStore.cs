namespace FileApi.Storage;

// Yerel disk depolama: {RootPath}/covers/{isbn} + {isbn}.ct sidecar (content-type).
// Sidecar S3 object-metadata semantiğini aynalar → backend swap kolay. CoverKey guard'lı.
// Backend seçimine göre Program'da elle kaydedilir (marker YOK — S3FileStore ile aynı IFileStore
// arayüzünü paylaşır, ikisini birden marker'la kaydetmek belirsizlik yaratır).
public sealed class LocalDiskFileStore : IFileStore
{
    private const string DefaultContentType = "application/octet-stream";
    private readonly string _coversDir;

    public LocalDiskFileStore(CoverStoreOptions options)
    {
        _coversDir = Path.Combine(options.RootPath, "covers");
        Directory.CreateDirectory(_coversDir);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct)
        => Task.FromResult(System.IO.File.Exists(PathFor(key)));

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var path = PathFor(key);
        await using (var fs = System.IO.File.Create(path))
        {
            await content.CopyToAsync(fs, ct);
        }
        await System.IO.File.WriteAllTextAsync(path + ".ct", contentType, ct);
    }

    public async Task<(Stream Content, string ContentType)?> TryGetAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        if (!System.IO.File.Exists(path)) return null;

        var ctPath = path + ".ct";
        var contentType = System.IO.File.Exists(ctPath)
            ? (await System.IO.File.ReadAllTextAsync(ctPath, ct)).Trim()
            : DefaultContentType;
        if (string.IsNullOrWhiteSpace(contentType)) contentType = DefaultContentType;

        Stream stream = System.IO.File.OpenRead(path);
        return (stream, contentType);
    }

    // Key her zaman güvenli olmalı — çağıran CoverKey'den geçirir; burada da savunma.
    private string PathFor(string key)
    {
        if (!CoverKey.TryCreate(key, out var safe))
            throw new ArgumentException($"Güvensiz cover key: {key}", nameof(key));
        return Path.Combine(_coversDir, safe);
    }
}
