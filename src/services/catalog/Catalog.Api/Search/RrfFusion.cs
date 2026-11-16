namespace Catalog.Api.Search;

// 011 US3: Reciprocal Rank Fusion — iki sıralamayı KONUM-tabanlı tek listede birleştirir.
// Ham skorlar (BM25 sınırsız + kosinüs 0-1) normalize EDİLMEZ; yalnız sıra sayılır. Saf fonksiyon
// (host'suz test edilebilir); füzyon takılabilir (FR-011 — başka birleştirme denenirse burası değişir).
public static class RrfFusion
{
    public const int DefaultK = 60; // sektör-default; büyük k üst-sıra tahakkümünü yumuşatır.

    /// <summary>İki sıralı aday listesini RRF ile birleştirir: skor(id)=Σ 1/(k+sıra). Bir listede
    /// olmayan id o listeden katkı almaz ama ELENMEZ. Azalan skora göre sıralı, tekil liste döner.
    /// Eşit skorda daha küçük en-iyi-sırada olan öne gelir (deterministik).</summary>
    public static IReadOnlyList<T> Combine<T>(
        IReadOnlyList<T> rankedA, IReadOnlyList<T> rankedB, int k = DefaultK) where T : notnull
    {
        var score = new Dictionary<T, double>();
        var bestRank = new Dictionary<T, int>();

        Accumulate(rankedA, k, score, bestRank);
        Accumulate(rankedB, k, score, bestRank);

        return score.Keys
            .OrderByDescending(id => score[id])
            .ThenBy(id => bestRank[id])
            .ToList();
    }

    private static void Accumulate<T>(
        IReadOnlyList<T> ranked, int k, Dictionary<T, double> score, Dictionary<T, int> bestRank)
        where T : notnull
    {
        for (var rank = 0; rank < ranked.Count; rank++)
        {
            var id = ranked[rank];
            score[id] = score.GetValueOrDefault(id) + 1.0 / (k + rank);
            if (!bestRank.TryGetValue(id, out var cur) || rank < cur)
                bestRank[id] = rank;
        }
    }
}
