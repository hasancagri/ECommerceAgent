using System.Globalization;
using ClosedXML.Excel;

namespace Catalog.Api.Import;

// US1/FR-001+FR-002: hosted xlsx yükleme ekranı — tek sayfalık gömülü HTML (Razor/SPA yok). ANONİM:
// token = yetki (İLKE V v1.11.1 capability-link istisnası; scope zorlaması link üretiminde, ImportCatalog).
// Bilinmeyen/tüketilmiş/süresi geçmiş token her iki uçta da NÖTR 404. Excel YALNIZ bir kez okunur:
// POST satırları staging'e (ImportRow) alır + token'ı tüketir; sonraki işleme tabloyu okur, Excel'i değil.
public static class ImportUploadEndpointExtension
{
    // Kilitli 14-kolon şeması (research D5). 12'si staging'e alınır; imageUrl (kapak async File.Api) +
    // discount (v1 dışı) KULLANILMAZ. 1-tabanlı ClosedXML kolon indexleri.
    private const int ColIsbn = 1, ColTitle = 2, ColAuthors = 3, ColPublisher = 4, ColPrice = 5,
        ColStock = 6, ColCategoryMid = 7, ColCategoryLeaf = 8, ColTags = 9, ColSpecs = 10,
        ColDescription = 11, ColFamilyCode = 13;

    public static void MapImportUploadEndpoints(this WebApplication app)
    {
        // GET: yükleme formu. Oturumu TÜKETMEZ (form açıp vazgeçmek linki öldürmez, süre öldürür).
        app.MapGet("/catalog-import/{token}", async (
            string token, IQuerySession session, CancellationToken ct) =>
        {
            var entry = await session.Query<ImportSession>()
                .FirstOrDefaultAsync(x => x.Token == token, ct);

            return entry is not null && entry.IsUsable(DateTimeOffset.UtcNow)
                ? Html(FormPage(token, error: null), StatusCodes.Status200OK)
                : Html(NotFoundPage, StatusCodes.Status404NotFound);
        });

        // POST: xlsx yükle → parse → staging + consume. Başarı token'ı tüketir; bozuk dosya oturumu YAŞATIR.
        app.MapPost("/catalog-import/{token}", async (
            string token, HttpRequest request, IMessageBus bus, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Html(FormPage(token, "Dosya bulunamadı. Lütfen bir .xlsx dosyası seçin."),
                    StatusCodes.Status400BadRequest);

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
                return Html(FormPage(token, "Dosya bulunamadı. Lütfen bir .xlsx dosyası seçin."),
                    StatusCodes.Status400BadRequest);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            var result = await bus.InvokeAsync<FeatureObjectResultModel<SubmitCatalogUpload.SubmitCatalogUploadResponse>>(
                new SubmitCatalogUpload.SubmitCatalogUploadCommand(token, ms.ToArray()), ct);

            if (result.IsSuccess)
                return Html(SuccessPage(result.Data!), StatusCodes.Status200OK);

            var messages = result.Messages ?? [];
            if (messages.Any(m => m.Code == CatalogResourceConstants.RECORD_NOT_FOUND
                                  || m.Code == CatalogResourceConstants.IMPORT_SESSION_NOT_USABLE))
                return Html(NotFoundPage, StatusCodes.Status404NotFound);

            return Html(FormPage(token,
                "Dosya okunamadı. Geçerli bir .xlsx (14 kolon; 1. kolon ISBN) yükleyin."),
                StatusCodes.Status400BadRequest);
        });
    }

    // T012/T013: xlsx parse + staging. [Transactional]: token doğrula → ClosedXML parse → her satır
    // ImportRow (ISBN varsa Pending, yoksa Failed) Store → token Consume(RowCount). AYNI commit (atomik).
    public static class SubmitCatalogUpload
    {
        public record SubmitCatalogUploadCommand(string Token, byte[] FileBytes);

        public class SubmitCatalogUploadResponse
        {
            public int Staged { get; set; }
            public int Invalid { get; set; }
        }

        [Transactional]
        public class SubmitCatalogUploadCommandHandler
        {
            public async Task<FeatureObjectResultModel<SubmitCatalogUploadResponse>> Handle(
                SubmitCatalogUploadCommand cmd,
                IDocumentSession session,
                CancellationToken ct)
            {
                var now = DateTimeOffset.UtcNow;
                var importSession = await session.Query<ImportSession>()
                    .FirstOrDefaultAsync(x => x.Token == cmd.Token, ct);

                // Bilinmeyen/tüketilmiş/süresi geçmiş token → nötr NotFound (token doğruluğu sızdırılmaz).
                if (importSession is null || !importSession.IsUsable(now))
                    return FeatureObjectResultModel<SubmitCatalogUploadResponse>.NotFound();

                List<ImportRow> rows;
                try
                {
                    rows = ParseRows(importSession.Id, cmd.FileBytes);
                }
                catch
                {
                    // Bozuk dosya / xlsx değil / şema tutmuyor → hata (oturum yaşar, admin düzeltip yeniden yükler).
                    return FeatureObjectResultModel<SubmitCatalogUploadResponse>.Error(new MessageItem
                    { Property = "file", Code = CatalogResourceConstants.IMPORT_ISBN_REQUIRED });
                }

                if (rows.Count == 0)
                    return FeatureObjectResultModel<SubmitCatalogUploadResponse>.Error(new MessageItem
                    { Property = "file", Code = CatalogResourceConstants.IMPORT_ISBN_REQUIRED });

                foreach (var row in rows)
                    session.Store(row);

                // Tek kullanım: başarılı yükleme oturumu tüketir + satır sayısı yazar.
                var consumed = importSession.Consume(rows.Count, now);
                if (!consumed.IsSuccess)
                    return FeatureObjectResultModel<SubmitCatalogUploadResponse>.NotFound();
                session.Store(importSession);

                return FeatureObjectResultModel<SubmitCatalogUploadResponse>.Ok(new SubmitCatalogUploadResponse
                {
                    Staged = rows.Count(r => r.Status == ImportRowStatus.Pending),
                    Invalid = rows.Count(r => r.Status == ImportRowStatus.Failed)
                });
            }

            // Kilitli 14-kolon şeması. Header satırı 1. kolonda "isbn" içermeli (şema guard'ı); yoksa
            // ArgumentException → bozuk dosya. Veri satırları 2'den. ISBN boşsa satır Failed staging.
            private static List<ImportRow> ParseRows(Guid importSessionId, byte[] fileBytes)
            {
                using var stream = new MemoryStream(fileBytes);
                using var workbook = new XLWorkbook(stream);
                var ws = workbook.Worksheets.First();

                var header = ws.Row(1);
                var headerIsbn = header.Cell(ColIsbn).GetString().Trim();
                if (!headerIsbn.Equals("isbn", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Kolon şeması uyuşmuyor (1. kolon ISBN olmalı).");

                var rows = new List<ImportRow>();
                foreach (var row in ws.RowsUsed().Skip(1))
                {
                    var isbn = row.Cell(ColIsbn).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(isbn))
                    {
                        rows.Add(ImportRow.CreateFailed(importSessionId, rawIsbn: null,
                            error: "Zorunlu alan ISBN boş — satır işlenemez."));
                        continue;
                    }

                    var created = ImportRow.Create(
                        importSessionId,
                        isbn,
                        row.Cell(ColTitle).GetString().Trim(),
                        row.Cell(ColAuthors).GetString().Trim(),
                        row.Cell(ColPublisher).GetString().Trim(),
                        ParseDecimal(row.Cell(ColPrice).GetString()),
                        ParseInt(row.Cell(ColStock).GetString()),
                        row.Cell(ColCategoryMid).GetString().Trim(),
                        row.Cell(ColCategoryLeaf).GetString().Trim(),
                        row.Cell(ColTags).GetString().Trim(),
                        row.Cell(ColSpecs).GetString().Trim(),
                        row.Cell(ColDescription).GetString().Trim(),
                        row.Cell(ColFamilyCode).GetString().Trim());

                    rows.Add(created.IsSuccess
                        ? created.Data!
                        : ImportRow.CreateFailed(importSessionId, isbn, "Satır doğrulanamadı."));
                }

                return rows;
            }

            // Fiyat/stok tolere edilir: nokta ya da virgül ondalık; boş/geçersiz → 0 (fiyatsız taslak kalır).
            private static decimal ParseDecimal(string raw)
            {
                raw = raw.Trim();
                if (string.IsNullOrEmpty(raw))
                    return 0m;
                if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ||
                    decimal.TryParse(raw, NumberStyles.Any, new CultureInfo("tr-TR"), out d))
                    return d < 0 ? 0m : d;
                return 0m;
            }

            private static int ParseInt(string raw)
            {
                raw = raw.Trim();
                if (int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var i))
                    return i < 0 ? 0 : i;
                return 0;
            }
        }
    }

    private static IResult Html(string html, int statusCode) =>
        Results.Content(html, "text/html; charset=utf-8", statusCode: statusCode);

    private static string Layout(string body) => $$"""
        <!DOCTYPE html>
        <html lang="tr">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="robots" content="noindex, nofollow">
        <title>Katalog Excel Import</title>
        <style>
        body { font-family: -apple-system, Segoe UI, Roboto, sans-serif; background: #f5f6f8; margin: 0;
               display: flex; justify-content: center; padding: 48px 16px; }
        .card { background: #fff; border-radius: 12px; box-shadow: 0 2px 12px rgba(0,0,0,.08);
                padding: 32px; max-width: 460px; width: 100%; }
        h1 { font-size: 1.2rem; margin: 0 0 8px; }
        p { color: #555; font-size: .92rem; line-height: 1.5; }
        input[type=file] { width: 100%; box-sizing: border-box; padding: 10px 12px; border: 1px solid #ccd;
                border-radius: 8px; font-size: .95rem; margin-top: 8px; }
        button { margin-top: 24px; width: 100%; padding: 12px; border: 0; border-radius: 8px;
                 background: #1a56db; color: #fff; font-size: 1rem; font-weight: 600; cursor: pointer; }
        .error { background: #fde8e8; color: #9b1c1c; border-radius: 8px; padding: 12px; font-size: .88rem; }
        .ok { background: #def7ec; color: #03543f; border-radius: 8px; padding: 12px; font-size: .9rem; }
        </style>
        </head>
        <body><div class="card">{{body}}</div></body>
        </html>
        """;

    private static string FormPage(string token, string? error) => Layout($"""
        <h1>Katalog Excel Import</h1>
        <p>Kilitli 14-kolonlu katalog dosyanızı (.xlsx) seçin ve yükleyin. Satırlar arka planda TASLAK
        ürüne dönüşür; yayın için ayrıca <strong>admin_publish_imported</strong> çağırın.
        Bu sayfa tek kullanımlıktır.</p>
        {(error is null ? "" : $"""<div class="error">{error}</div>""")}
        <form method="post" action="/catalog-import/{token}" enctype="multipart/form-data">
          <input type="file" name="file" accept=".xlsx" required>
          <button type="submit">Yükle</button>
        </form>
        """);

    private static string SuccessPage(SubmitCatalogUpload.SubmitCatalogUploadResponse r) => Layout($"""
        <h1>Alındı ✓</h1>
        <div class="ok">{r.Staged} satır işleme alındı{(r.Invalid > 0 ? $", {r.Invalid} satır hatalı (ISBN eksik)" : "")}.
        Ürünler arka planda TASLAK olarak oluşturuluyor.</div>
        <p>Durumu <strong>admin_get_import_status</strong> ile izleyebilirsiniz. Bu sayfayı kapatabilirsiniz;
        link artık geçersizdir.</p>
        """);

    private const string NotFoundPage = """
        <!DOCTYPE html>
        <html lang="tr"><head><meta charset="utf-8"><meta name="robots" content="noindex, nofollow">
        <title>Sayfa bulunamadı</title></head>
        <body style="font-family: sans-serif; text-align: center; padding-top: 15vh; color: #555;">
        <h1>Sayfa bulunamadı</h1>
        <p>Aradığınız bağlantı geçersiz ya da artık kullanılamıyor.</p>
        </body></html>
        """;
}
