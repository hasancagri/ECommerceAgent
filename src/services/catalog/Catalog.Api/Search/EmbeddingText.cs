using System.Security.Cryptography;
using System.Text;
using Catalog.Api.Domains.Products;

namespace Catalog.Api.Search;

// 025 Dilim-1 (İlke VI test-first): bir kitabın embed girdisi. SAF — yan etki yok. Yalnız ANLAM taşıyan
// alanlar: başlık + 008 zengin açıklama + tür/ton/konu (BookTaxonomy). Fiyat/stok/indirim (değişken),
// dil/sayfa/cilt/yayınevi/ISBN (yapısal filtre), AudienceLevel (hard filtre — çift-sayım) ve yazar
// (v1'de dışarıda) GİRMEZ. Hafif etiketleme modele yapı verir; açıklama gövdeyi taşır.
public static class EmbeddingText
{
    public static string Build(
        string? name, string? description, string genre,
        IReadOnlyList<string> moods, IReadOnlyList<string> themes)
    {
        var lines = new List<string> { $"Başlık: {name?.Trim()}" };

        var g = genre?.Trim();
        if (!string.IsNullOrEmpty(g) && g != BookTaxonomy.Unknown)
            lines.Add($"Tür: {g}");

        var moodList = Join(moods);
        if (moodList.Length > 0)
            lines.Add($"Ruh hali: {moodList}");

        var themeList = Join(themes);
        if (themeList.Length > 0)
            lines.Add($"Konular: {themeList}");

        var desc = description?.Trim();
        if (!string.IsNullOrEmpty(desc))
            lines.Add($"Açıklama: {desc}");

        return string.Join("\n", lines);
    }

    // Embed girdisinin kimliği — değişmediyse yeniden-embed atlanır (maliyet + idempotent). SHA256 hex.
    public static string Hash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexStringLower(bytes);
    }

    private static string Join(IReadOnlyList<string> values) =>
        string.Join(", ", values.Select(v => v?.Trim()).Where(v => !string.IsNullOrEmpty(v)));
}
