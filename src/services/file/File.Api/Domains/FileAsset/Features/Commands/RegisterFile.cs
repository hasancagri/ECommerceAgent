namespace FileApi.Domains.FileAsset.Features.Commands;

// US1: dosya yaz + kayıt defterine düşür (senkron). Fiziki bitler IFileStore backend'ine (R2), sonra
// FileAsset upsert (yok: Create / var: AddOrReplaceLocation — aynı StorageType path'i günceller, ikinci
// satır yok). Dönüşte çözümlenmiş URL. İdempotent: aynı imageName+storageType → path güncellenir.
public static class RegisterFile
{
    public record RegisterFileCommand(
        string ImageName, string ContentType, byte[] Content, StorageType? StorageType);

    public class RegisterFileResult
    {
        public string ImageName { get; set; } = default!;
        public string? Url { get; set; }
    }

    public class RegisterFileCommandHandler(
        IDocumentSession session, IFileStore fileStore, CoverUrlResolver resolver)
    {
        [Transactional]
        public async Task<FeatureObjectResultModel<RegisterFileResult>> Handle(RegisterFileCommand cmd, CancellationToken ct)
        {
            if (!CoverKey.TryCreate(cmd.ImageName, out var key))
                return FeatureObjectResultModel<RegisterFileResult>.Error(
                    new MessageItem { Code = FileApiResourceConstants.FILE_IMAGENAME_INVALID });

            if (cmd.Content is null || cmd.Content.Length == 0)
                return FeatureObjectResultModel<RegisterFileResult>.Error(
                    new MessageItem { Code = FileApiResourceConstants.FILE_CONTENT_EMPTY });

            var storageType = cmd.StorageType ?? resolver.DefaultStorageType;
            var contentType = string.IsNullOrWhiteSpace(cmd.ContentType) ? "application/octet-stream" : cmd.ContentType;

            // Fiziki yaz (backend R2). Key = ImageName (kapakta ISBN); StorageFilePath = key.
            await using (var stream = new MemoryStream(cmd.Content, writable: false))
                await fileStore.PutAsync(key, stream, contentType, ct);

            // Kayıt defteri upsert.
            var asset = await session.Query<FileAsset>().FirstOrDefaultAsync(a => a.ImageName == cmd.ImageName, ct);
            if (asset is null)
            {
                var created = FileAsset.Create(cmd.ImageName, contentType, cmd.Content.Length, storageType, key);
                if (!created.IsSuccess) return FeatureObjectResultModel<RegisterFileResult>.Error(created.Messages);
                asset = created.Data!;
            }
            else
            {
                var upsert = asset.AddOrReplaceLocation(storageType, key, contentType, cmd.Content.Length);
                if (!upsert.IsSuccess) return FeatureObjectResultModel<RegisterFileResult>.Error(upsert.Messages);
            }
            session.Store(asset);

            var url = resolver.Resolve(storageType, key);
            return FeatureObjectResultModel<RegisterFileResult>.Ok(new RegisterFileResult { ImageName = cmd.ImageName, Url = url });
        }
    }
}