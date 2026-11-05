using Catalog.Api.Filters;

namespace Catalog.Api.Domains.ImportJobs;

// 004: ilk REST admin yüzeyi (contracts/rest-import.md). Dış yol gateway'le AYNI (/api/admin/catalog
// — path rewrite yok); kenar yetki gateway'de, buradaki filtre ikinci kat (FR-014, T017).
public static class ImportJobEndpointExtension
{
    public static void MapImportJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/catalog/imports")
            .WithTags("CatalogImportAdmin")
            .AddEndpointFilter<AdminApiKeyEndpointFilter>();

        // POST / — multipart .xlsx → 202 + jobId. İki-faz: önce dry-run (hiç yazmaz), rapor
        // GET /{jobId}'den okunur, yazım POST /{jobId}/confirm ile başlar.
        group.MapPost("/", async (HttpRequest request, IMessageBus bus, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest("multipart/form-data bekleniyor.");

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
                return Results.BadRequest("'file' alanında .xlsx bekleniyor.");
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("Yalnız .xlsx kabul edilir.");

            // Diske yaz — dry-run da onay sonrası yazım da aynı dosyayı okur; dosya SİLİNMEZ
            // (kullanıcı kararı), iş kaydının diskteki izi olarak kalır.
            var tempDir = Path.Combine(Path.GetTempPath(), "catalog-imports");
            Directory.CreateDirectory(tempDir);
            var tempPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}.xlsx");
            await using (var target = System.IO.File.Create(tempPath))
                await file.CopyToAsync(target, ct);

            var result = await bus.InvokeAsync<FeatureObjectResultModel<StartImport.StartImportResponse>>(
                new StartImport.StartImportCommand(file.FileName, tempPath), ct);

            if (result.IsSuccess)
                return Results.Accepted($"/api/admin/catalog/imports/{result.Data.JobId}", result.Data);

            var alreadyRunning = result.Messages != null && result.Messages.Any(m => m.Code == CatalogResourceConstants.IMPORT_ALREADY_RUNNING);
            return alreadyRunning ? Results.Conflict(result) : Results.BadRequest(result);
        });

        // POST /{jobId}/confirm — dry-run raporu onayı: gerçek yazım ancak bununla başlar (Faz 2).
        group.MapPost("/{jobId:guid}/confirm", async (Guid jobId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<ConfirmImport.ConfirmImportResponse>>(
                new ConfirmImport.ConfirmImportCommand(jobId), ct);

            if (result.IsSuccess)
                return Results.Accepted($"/api/admin/catalog/imports/{jobId}", result.Data);

            var notFound = result.Messages != null && result.Messages.Any(m => m.Code == CatalogResourceConstants.IMPORT_JOB_NOT_FOUND);
            return notFound ? Results.NotFound(result) : Results.Conflict(result);
        });

        // GET /{jobId} — durum + sayaçlar (FR-006).
        group.MapGet("/{jobId:guid}", async (Guid jobId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<GetImportStatus.ImportStatusResponse>>(
                new GetImportStatus.GetImportStatusQuery(jobId), ct);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
        });

        // GET /{jobId}/errors — sebepli satır hataları (FR-005).
        group.MapGet("/{jobId:guid}/errors", async (Guid jobId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<FeatureListResultModel<GetImportErrors.ImportErrorView>>(
                new GetImportErrors.GetImportErrorsQuery(jobId), ct);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
        });
    }

}
