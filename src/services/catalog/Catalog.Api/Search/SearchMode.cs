namespace Catalog.Api.Search;

// 018: birleşik aramanın yürütme modu — SearchRequest'ten türetilir (saklanmaz). Tek mod:
// alaka (BM25 skoru) ile alan-sıralaması aynı sorguda UYGULANMAZ (FR-004).
public enum SearchMode
{
    Relevance,      // metin var, açık sort yok → ORDER BY pdb.score
    FieldSort,      // açık sort var → ORDER BY <fast-field> (alaka ezilir)
    DefaultBrowse,  // metin de sort da yok → ORDER BY PurchaseCount DESC (popülerlik)
}

public static class SearchModeResolver
{
    /// <summary>Metin varlığı + açık sıralamadan tek yürütme modunu türetir. Açık sort her zaman
    /// FieldSort (alakayı ezer); sort yoksa metin varsa Relevance, ikisi de yoksa DefaultBrowse.</summary>
    public static SearchMode Resolve(bool hasText, SortOption? sort)
    {
        if (sort is not null) return SearchMode.FieldSort;
        return hasText ? SearchMode.Relevance : SearchMode.DefaultBrowse;
    }
}
