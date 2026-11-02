namespace FileApi.Constants;

// File.Api hata kodları (serbest metin değil sabit; MessageItem.Code). Her servis kendi kodlarına sahip.
public static class FileApiResourceConstants
{
    // Geçersiz/güvensiz ImageName (CoverKey guard — boş/traversal/ayraç/boşluk).
    public const string FILE_IMAGENAME_INVALID = "FILE_IMAGENAME_INVALID";

    // Konumsuz FileAsset yok — Create en az bir konum ister (invariant 2).
    public const string FILE_LOCATION_REQUIRED = "FILE_LOCATION_REQUIRED";

    // Son konum silinemez (invariant 2).
    public const string FILE_LOCATION_LAST_CANNOT_REMOVE = "FILE_LOCATION_LAST_CANNOT_REMOVE";

    // Geçersiz/boş StorageFilePath.
    public const string FILE_STORAGE_PATH_INVALID = "FILE_STORAGE_PATH_INVALID";

    // Boş içerik yazılamaz (RegisterFile).
    public const string FILE_CONTENT_EMPTY = "FILE_CONTENT_EMPTY";
}