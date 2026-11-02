using JsonSerializer = System.Text.Json.JsonSerializer;
using JsonSerializerOptions = System.Text.Json.JsonSerializerOptions;

namespace FileApi.Migration;

// 091: commit'li seed books.json'dan yalnız {isbn, imageUrl} okur (XlsxCoverSource'un halefi —
// repo-dışı xlsx bağımlılığı bitti). Dosya Catalog seeder'la PAYLAŞILAN tek gerçek-kaynak
// (sözleşme specs/091-catalog-json-seed/contracts/seed-record.md); bilinmeyen alanlar yok sayılır.
public sealed class JsonCoverSource
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class Row
    {
        public string Isbn { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
    }

    public IEnumerable<(string Isbn, string? ImageUrl)> Read(string jsonPath)
    {
        using var stream = System.IO.File.OpenRead(jsonPath);
        var rows = JsonSerializer.Deserialize<List<Row>>(stream, JsonOptions) ?? [];

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Isbn))
                continue;
            yield return (row.Isbn.Trim(), row.ImageUrl?.Trim());
        }
    }
}