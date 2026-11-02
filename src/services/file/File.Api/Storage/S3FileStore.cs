using Amazon.S3;
using Amazon.S3.Model;

namespace FileApi.Storage;

// Cloudflare R2 (S3-uyumlu) IFileStore impl'i. Key=isbn; content-type S3 NATIVE metadata
// (yerel diskteki .ct sidecar YOK). Serve/migration kontratı LocalDiskFileStore ile aynı (FR-008).
public sealed class S3FileStore : IFileStore
{
    private const string DefaultContentType = "application/octet-stream";
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public S3FileStore(IAmazonS3 s3, R2Options options)
    {
        _s3 = s3;
        _bucket = options.BucketName;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct)
    {
        var safe = SafeKey(key);
        try
        {
            await _s3.GetObjectMetadataAsync(_bucket, safe, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = SafeKey(key),
            InputStream = content,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? DefaultContentType : contentType,
            DisablePayloadSigning = true   // R2: streaming imzalı payload'ı desteklemez
        };
        await _s3.PutObjectAsync(request, ct);
    }

    public async Task<(Stream Content, string ContentType)?> TryGetAsync(string key, CancellationToken ct)
    {
        var safe = SafeKey(key);
        try
        {
            var resp = await _s3.GetObjectAsync(_bucket, safe, ct);
            var contentType = string.IsNullOrWhiteSpace(resp.Headers.ContentType)
                ? DefaultContentType
                : resp.Headers.ContentType;
            return (resp.ResponseStream, contentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // Çağıran CoverKey'den geçirir; burada da savunma (traversal/güvensiz key reddi).
    private static string SafeKey(string key)
    {
        if (!CoverKey.TryCreate(key, out var safe))
            throw new ArgumentException($"Güvensiz cover key: {key}", nameof(key));
        return safe;
    }
}