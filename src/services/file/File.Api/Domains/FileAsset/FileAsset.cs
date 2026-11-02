namespace FileApi.Domains.FileAsset;

// Hangi fiziki depo bir konumu barındırıyor. Bir mantıksal dosya birden çok tipte yaşayabilir (redundancy).
public enum StorageType
{
    R2,                 // Cloudflare R2 (S3-uyumlu) — bugünkü kanonik depo
    Local,              // yerel disk (dev/fallback)
    S3,                 // AWS S3
    CloudflareImages,   // opaque provider (StorageFilePath = opaque ID/URL)
    B2                  // Backblaze B2 (offsite mirror adayı)
}

// Bir mantıksal dosyanın tek fiziki konumu. Aggregate içinde nested (ayrı tablo yok — Marten JSON listesi).
// Base ALMAZ (sade entity). Mutasyon yalnız FileAsset metodundan.
public sealed class FileStorageLocation
{
    // Marten (Newtonsoft, non-public ctor) için.
    private FileStorageLocation() { }

    private FileStorageLocation(StorageType storageType, string storageFilePath)
    {
        Id = Guid.NewGuid();
        StorageType = storageType;
        StorageFilePath = storageFilePath;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public StorageType StorageType { get; private set; }

    // Backend'in dosyayı bulmak için ihtiyaç duyduğu key/path (R2'de ISBN; opaque provider'da opaque ID).
    // Full URL DEĞİL — URL, StorageType + config-base ile çözümlenir (CoverUrlResolver).
    public string StorageFilePath { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }

    // Aggregate-içi fabrika + upsert. FileAsset dışından çağrılmaz.
    internal static FileStorageLocation Create(StorageType storageType, string storageFilePath)
        => new(storageType, storageFilePath);

    internal void UpdatePath(string storageFilePath) => StorageFilePath = storageFilePath;
}

// Kayıt defteri girdisi. ImageName tekil/değişmez mantıksal anahtar (kapakta ISBN); metadata +
// çoklu fiziki konum. Byte tutmaz (fiziki bitler IFileStore backend'inde). Marten dokümanı.
public class FileAsset : AggregateRoot
{
    private readonly List<FileStorageLocation> _locations = new();

    // Marten (Newtonsoft, non-public ctor) için.
    protected FileAsset() { }

    private FileAsset(string imageName, string contentType, long sizeBytes)
    {
        ImageName = imageName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
    }

    // Mantıksal anahtar — UNIQUE index; setter yok (invariant 3: değişmez + tekil).
    public string ImageName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }

    // Okuma yalnız IReadOnlyList; mutasyon aggregate metodundan (İLKE II).
    public IReadOnlyList<FileStorageLocation> Locations => _locations;

    /// <summary>
    /// Yeni kayıt defteri girdisi. En az bir konum ZORUNLU (invariant 2); ImageName güvensiz/boş reddi
    /// (CoverKey guard, invariant 3). İlk konum verilen tip+path ile eklenir.
    /// </summary>
    public static ResultDomain<FileAsset> Create(
        string imageName, string contentType, long sizeBytes,
        StorageType firstStorageType, string firstStorageFilePath)
    {
        if (!CoverKey.TryCreate(imageName, out _))
            return ResultDomain<FileAsset>.Error(new MessageItem { Code = FileApiResourceConstants.FILE_IMAGENAME_INVALID });

        if (string.IsNullOrWhiteSpace(firstStorageFilePath))
            return ResultDomain<FileAsset>.Error(new MessageItem { Code = FileApiResourceConstants.FILE_STORAGE_PATH_INVALID });

        var asset = new FileAsset(imageName, contentType ?? string.Empty, sizeBytes);
        asset._locations.Add(FileStorageLocation.Create(firstStorageType, firstStorageFilePath));
        return ResultDomain<FileAsset>.Ok(asset);
    }

    /// <summary>
    /// Verilen StorageType için konumu upsert eder: aynı tip varsa path'i günceller (ikinci satır YOK,
    /// invariant 1), yoksa yeni konum ekler. Metadata (ContentType/SizeBytes) da tazelenir.
    /// </summary>
    public ResultDomain AddOrReplaceLocation(StorageType storageType, string storageFilePath, string? contentType = null, long? sizeBytes = null)
    {
        if (string.IsNullOrWhiteSpace(storageFilePath))
            return ResultDomain.Error(new MessageItem { Code = FileApiResourceConstants.FILE_STORAGE_PATH_INVALID });

        var existing = _locations.FirstOrDefault(l => l.StorageType == storageType);
        if (existing is not null)
            existing.UpdatePath(storageFilePath);
        else
            _locations.Add(FileStorageLocation.Create(storageType, storageFilePath));

        if (!string.IsNullOrWhiteSpace(contentType)) ContentType = contentType!;
        if (sizeBytes.HasValue) SizeBytes = sizeBytes.Value;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Bir depodaki konumu çıkarır. Son konumsa reddedilir (invariant 2 — konumsuz asset yok).</summary>
    public ResultDomain RemoveLocation(StorageType storageType)
    {
        var existing = _locations.FirstOrDefault(l => l.StorageType == storageType);
        if (existing is null)
            return ResultDomain.Ok();   // yoksa no-op (idempotent)

        if (_locations.Count == 1)
            return ResultDomain.Error(new MessageItem { Code = FileApiResourceConstants.FILE_LOCATION_LAST_CANNOT_REMOVE });

        _locations.Remove(existing);
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>
    /// Çözümleme için tercih edilen konum: verilen default StorageType'ın konumu varsa onu, yoksa
    /// mevcut ilk konumu döner. Saf getter (asla null — en az bir konum invariant'ı garanti).
    /// </summary>
    public FileStorageLocation PreferredLocation(StorageType defaultStorageType)
        => _locations.FirstOrDefault(l => l.StorageType == defaultStorageType) ?? _locations[0];
}