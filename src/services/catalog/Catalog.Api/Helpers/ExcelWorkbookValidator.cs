namespace Catalog.Api.Helpers;

// 004 FR-400 ön kapısı: çalışma kitabı açılabiliyor mu + her sayfada zorunlu sütunlar var mı.
// Satır İŞLEMEZ/doğrulamaz — satır-düzeyi kurallar ExcelRowMapper'da. Dönüş: null = geçerli,
// string = insan-okur hata (IMPORT_FILE_INVALID param'ı olur).
public static class ExcelWorkbookValidator
{
    // Zorunlu sütunlar: eşlemenin kimlik/künye asgarisi (research R2). Eksikse hiçbir satır işlenmez.
    private static readonly string[] RequiredColumns = { "Kitap Adı", "Yazar", "ISBN" };

    public static string? Validate(string path)
    {
        try
        {
            using var workbook = new XLWorkbook(path);
            if (!workbook.Worksheets.Any())
                return "Çalışma kitabında sayfa yok.";

            foreach (var sheet in workbook.Worksheets)
            {
                var headers = sheet.Row(1).CellsUsed().Select(c => c.GetString().Trim()).ToHashSet();
                var missing = RequiredColumns.Where(c => !headers.Contains(c)).ToList();
                if (missing.Count > 0)
                    return $"'{sheet.Name}' sayfasında zorunlu sütun(lar) eksik: {string.Join(", ", missing)}";
            }
            return null;
        }
        catch (Exception)
        {
            return "Dosya .xlsx olarak açılamadı.";
        }
    }
}