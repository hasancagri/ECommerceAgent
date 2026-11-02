using FileApi.Domains.FileAsset.Features.Commands;
using FileApi.Domains.FileAsset.Features.Queries;
using Microsoft.AspNetCore.Http;

namespace FileApi.Domains.FileAsset;

// Kayıt defteri S2S yüzeyi (internal; makine kimliği — yeni scope yok). Serve (anonim kapak)
// CoverEndpoints'te ayrı kalır. register = yaz+kayıt; resolve = batch URL çözümleme; locations = redundancy.
public static class FileAssetEndpointExtension
{
    public sealed record ResolveRequest(List<string> ImageNames);
    public sealed record LocationView(string StorageType, string StorageFilePath, DateTimeOffset CreatedAt);

    public static void MapFileAssetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/files").WithTags("FileRegistryInternal");

        // US1: dosya yaz + kayıt (multipart: file + imageName + contentType? + storageType?). 200 {imageName,url}.
        group.MapPost("/", async (HttpRequest request, IMessageBus bus, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest("multipart/form-data bekleniyor.");

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            var imageName = form["imageName"].ToString();
            if (file is null || string.IsNullOrWhiteSpace(imageName))
                return Results.BadRequest();

            var contentType = form["contentType"].ToString();
            if (string.IsNullOrWhiteSpace(contentType)) contentType = file.ContentType;

            StorageType? storageType = null;
            var st = form["storageType"].ToString();
            if (!string.IsNullOrWhiteSpace(st) && Enum.TryParse<StorageType>(st, ignoreCase: true, out var parsed))
                storageType = parsed;

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            var result = await bus.InvokeAsync<FeatureObjectResultModel<RegisterFile.RegisterFileResult>>(
                new RegisterFile.RegisterFileCommand(imageName, contentType, ms.ToArray(), storageType), ct);

            return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
        });

        // US2: batch ImageName → URL (lokal, 0 dış çağrı). Kayıtsız → {url:null, exists:false}.
        group.MapPost("/resolve", async (ResolveRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<ResolveUrls.ResolveUrlsResult>>(
                new ResolveUrls.ResolveUrlsQuery(body?.ImageNames ?? new()), ct);

            return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
        });

        // US3 (FR-010): bir dosyanın konumları (redundancy görünürlüğü). Kayıtsız → 404.
        group.MapGet("/{imageName}/locations", async (string imageName, IQuerySession session, CancellationToken ct) =>
        {
            var asset = await session.Query<FileAsset>().FirstOrDefaultAsync(a => a.ImageName == imageName, ct);
            if (asset is null) return Results.NotFound();

            var views = asset.Locations
                .Select(l => new LocationView(l.StorageType.ToString(), l.StorageFilePath, l.CreatedAt))
                .ToList();
            return Results.Ok(views);
        });
    }
}