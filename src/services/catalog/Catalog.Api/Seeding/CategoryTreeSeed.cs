namespace Catalog.Api.Seeding;

// 095 Faz A saf çekirdeği: kanonik kategori yollarından ([üst] ya da [üst, leaf]) deterministik düğüm
// listesi üretir — ebeveynler çocuklardan ÖNCE gelir (ilk-görülme sırası korunur). Kapalı ağaç yalnız
// buradan kurulur (FR-007). Ad ağaç-global unique varsayımı (spec Assumptions) İHLALDE
// InvalidOperationException (fail-fast: bozuk commit'li veri sessiz geçilmez); >2 seviye de hata.
// leaf==üst savunma guard'ı: normalize eşitse tek kök düğüme katlanır.
public static class CategoryTreeSeed
{
    public record Node(string Name, string? ParentName);

    public static IReadOnlyList<Node> Build(IEnumerable<IReadOnlyList<string>> paths)
    {
        var roots = new List<Node>(); // ilk-görülme sırası
        var children = new List<Node>();
        // normalized ad → (isRoot, parentNormalized) — ağaç-global unique denetimi.
        var seen = new Dictionary<string, (bool IsRoot, string? ParentNormalized)>();

        foreach (var path in paths)
        {
            var segments = path
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();
            if (segments.Count == 0)
                continue;
            if (segments.Count > 2)
                throw new InvalidOperationException(
                    $"Kategori yolu 2 seviyeyi aşıyor: [{string.Join(" > ", segments)}] (kanonik veri [üst] ya da [üst, leaf]).");

            var rootName = segments[0];
            var rootKey = NameNormalization.Normalize(rootName);
            AddNode(rootName, rootKey, parentName: null, parentKey: null);

            if (segments.Count == 2)
            {
                var leafName = segments[1];
                var leafKey = NameNormalization.Normalize(leafName);
                if (leafKey == rootKey)
                    continue; // leaf==üst → kök düğüme katlan
                AddNode(leafName, leafKey, rootName, rootKey);
            }
        }

        return roots.Concat(children).ToList();

        void AddNode(string name, string key, string? parentName, string? parentKey)
        {
            if (seen.TryGetValue(key, out var existing))
            {
                if (existing.IsRoot != (parentKey is null) || existing.ParentNormalized != parentKey)
                    throw new InvalidOperationException(
                        $"Kategori adı ağaç-global unique değil: '{name}' hem '{existing.ParentNormalized ?? "KÖK"}' hem '{parentKey ?? "KÖK"}' altında görüldü.");
                return;
            }

            seen[key] = (parentKey is null, parentKey);
            if (parentKey is null)
                roots.Add(new Node(name, null));
            else
                children.Add(new Node(name, parentName));
        }
    }
}