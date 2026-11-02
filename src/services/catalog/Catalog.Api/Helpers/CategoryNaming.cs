namespace Catalog.Api.Helpers;

// 095: yayın-anı kategori zincir çözümü — fat ProductChangedEvent'in Categories (adlar) + CategoryIds
// (id'ler) alanlarının TEK kaynağı (tek çağıran seed Faz B — 097'de FileConsumers söküldü).
// Leaf-önce sıra: [leaf, ata]. Related kategoriler zincire GİRMEZ (FR-005 — çağıran yalnız primary verir).
public static class CategoryNaming
{
    /// <summary>Leaf'ten köke yürüyerek (ids, adlar) zincirini döner. Haritada olmayan düğümde durur
    /// (savunma); bilinmeyen leaf → boş listeler.</summary>
    public static (List<Guid> Ids, List<string> Names) ResolveChain(
        Guid leafCategoryId, IReadOnlyDictionary<Guid, Category> all)
    {
        var ids = new List<Guid>();
        var names = new List<string>();
        var current = leafCategoryId;
        while (current != Guid.Empty && all.TryGetValue(current, out var category) && !ids.Contains(current))
        {
            ids.Add(current);
            names.Add(category.Name);
            current = category.ParentCategoryId ?? Guid.Empty;
        }
        return (ids, names);
    }
}
