namespace Catalog.Api.Search;

// 021 (FR-014): arama facet değerleri kapalı sözlüğe karşı doğrulanır — sözlük-dışı değer sessizce
// "hiçbir şey tutmaz" yerine anlaşılır hatayla reddedilir. Saf birim (BookTaxonomy sözlüğü); REST +
// MCP yüzeyleri paylaşır. Kullanıcı sorgusunu YORUMLAMAZ (o LLM'in işi) — yalnız tam-değer kapısı.
public static class TaxonomyFacetValidator
{
    /// <summary>Verilen facet dizilerinde sözlük-dışı İLK değeri döner; hepsi geçerliyse null.
    /// Boş/null dizi atlanır (facet uygulanmamış demektir).</summary>
    public static string? FirstInvalid(
        IReadOnlyList<string>? genres, IReadOnlyList<string>? moods,
        IReadOnlyList<string>? audienceLevels, IReadOnlyList<string>? themes)
    {
        return FirstNotIn(genres, BookTaxonomy.IsValidGenre)
            ?? FirstNotIn(moods, BookTaxonomy.IsValidMood)
            ?? FirstNotIn(audienceLevels, BookTaxonomy.IsValidAudienceLevel)
            ?? FirstNotIn(themes, BookTaxonomy.IsValidTheme);
    }

    private static string? FirstNotIn(IReadOnlyList<string>? values, Func<string, bool> isValid)
    {
        if (values is null) return null;
        foreach (var v in values)
            if (!isValid(v)) return v;
        return null;
    }
}
