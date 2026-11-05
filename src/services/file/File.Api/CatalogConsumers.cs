using FileApi.Domains.FileAsset.Features.Commands;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace FileApi;

// US2/T019: Catalog'un ProductAdded event'ini tüketir (kaynak=Catalog → conventions kaynak-adı kuralı).
// ISBN'in kapağını çözer: R2'de hazırsa URL'i doğrudan; değilse yerel staging'den ({RootPath}/covers/{isbn})
// okuyup R2'ye yükler + kayıt defterine düşürür (RegisterFile). Kapak bulunursa CoverIngested(isbn,url) yayar
// (Catalog SetImage'e köprü); bulunmazsa event YOK (ürün placeholder'da kalır — import bloklanmaz, FR-008).
public class CatalogConsumers(
    IFileStore fileStore,
    CoverUrlResolver resolver,
    CoverStoreOptions coverStore,
    IMessageBus bus,
    IHttpClientFactory httpClientFactory,
    ILogger<CatalogConsumers> logger)
{
    public async Task Handle(IntegrationEvents.ProductAdded message, CancellationToken ct)
    {
        var isbn = message.Barcode;
        if (!CoverKey.TryCreate(isbn, out var key))
            return; // güvensiz anahtar → kapak çözülemez (no-op)

        // 1) R2'de zaten var mı? Varsa doğrudan çöz — İNDİRME ATLANIR (004 idempotentlik; 082 backfill yolu).
        if (await fileStore.ExistsAsync(key, ct))
        {
            var existingUrl = resolver.Resolve(resolver.DefaultStorageType, key);
            if (!string.IsNullOrWhiteSpace(existingUrl))
                await bus.PublishAsync(new IntegrationEvents.CoverIngested(isbn, existingUrl));
            return;
        }

        // 2) 004: event kapak kaynağı taşıyorsa (Excel "Kapak URL") dış adresten indir.
        //    İndirilemezse ürün kapaksız kalır — iş durmaz (FR-011; fırlatmak DLQ'yu boşa doldurur).
        if (!string.IsNullOrWhiteSpace(message.CoverSourceUrl))
        {
            byte[] downloaded;
            string downloadedContentType;
            try
            {
                var http = httpClientFactory.CreateClient("cover-download");
                using var response = await http.GetAsync(message.CoverSourceUrl, ct);
                response.EnsureSuccessStatusCode();
                downloaded = await response.Content.ReadAsByteArrayAsync(ct);
                downloadedContentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cover download failed for ISBN {Isbn}.", isbn);
                return;
            }

            var registered = await bus.InvokeAsync<FeatureObjectResultModel<RegisterFile.RegisterFileResult>>(
                new RegisterFile.RegisterFileCommand(isbn, downloadedContentType, downloaded, StorageType: null), ct);
            if (registered.IsSuccess && !string.IsNullOrWhiteSpace(registered.Data?.Url))
                await bus.PublishAsync(new IntegrationEvents.CoverIngested(isbn, registered.Data.Url));
            else
                logger.LogWarning("Cover register failed for ISBN {Isbn} (download path).", isbn);
            return;
        }

        // 3) Eski davranış: yerel staging'den yükle. Yoksa placeholder — event yok.
        var coversDir = Path.Combine(coverStore.RootPath, "covers");
        var localPath = Path.Combine(coversDir, key);
        if (!System.IO.File.Exists(localPath))
            return;

        var bytes = await System.IO.File.ReadAllBytesAsync(localPath, ct);
        var contentType = await ReadContentTypeAsync(localPath, ct);

        var result = await bus.InvokeAsync<FeatureObjectResultModel<RegisterFile.RegisterFileResult>>(
            new RegisterFile.RegisterFileCommand(isbn, contentType, bytes, StorageType: null), ct);

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Data?.Url))
        {
            logger.LogWarning("Cover register failed for ISBN {Isbn}.", isbn);
            return;
        }

        await bus.PublishAsync(new IntegrationEvents.CoverIngested(isbn, result.Data.Url));
    }

    // LocalDiskFileStore emsali: content-type yan-dosyada ({path}.ct); yoksa octet-stream.
    private static async Task<string> ReadContentTypeAsync(string localPath, CancellationToken ct)
    {
        var sidecar = localPath + ".ct";
        if (!System.IO.File.Exists(sidecar))
            return "application/octet-stream";
        var value = (await System.IO.File.ReadAllTextAsync(sidecar, ct)).Trim();
        return string.IsNullOrWhiteSpace(value) ? "application/octet-stream" : value;
    }
}
