namespace Catalog.Api.Domains.Categories;

// Çoklu-kategori ES projeksiyonu için ad çözümleyici: verilen kategori Id'leri + TÜM ata zincirlerinin
// adlarını (dedupe) döner. Böylece "Edebiyat > Roman (Çeviri)" kitabı hem "Roman (Çeviri)" hem "Edebiyat"
// filtresine düşer. Seed zinciri inline kurar; admin yayın noktaları (create/update/republish) bunu kullanır
// (tek kaynak — Specs projeksiyonundaki gibi drift riskine karşı).
public static class CategoryNaming
{
    public static List<string> Resolve(IEnumerable<Guid> categoryIds, IReadOnlyDictionary<Guid, Category> all)
    {
        var names = new List<string>();
        var seen = new HashSet<Guid>();
        foreach (var id in categoryIds)
        {
            var current = id;
            while (current != Guid.Empty && seen.Add(current) && all.TryGetValue(current, out var cat))
            {
                names.Add(cat.Name);
                current = cat.ParentCategoryId ?? Guid.Empty;
            }
        }
        return names.Distinct().ToList();
    }
}
