using System.Globalization;

namespace Catalog.Api.Search;

// 018 (İlke VI, T010): SearchRequest → ParadeDB @@@ clause + SQL predicate'leri saf kurar (DB'siz,
// host'suz test edilebilir). Yapısal filtre + text OR-grupları tek POZİTİF clause'ta birleşir (AND);
// clause boşsa null → çağıran `paradedb.all()` kullanır. Canlı kontrat (research.md T010 kilidi):
// range/term/uuid-quoted pozitif çalışır; `NOT field:"x"` string query'de BOZUK → ExcludePublisher
// SQL predicate'e çıkar (clause POZİTİF kalır). Publisher/ExcludePublisher ADI handler'da Marten ile
// id'ye çözülüp verilir (builder saf kalır — ad→id DB işi burada YOK).
public static class SearchPredicateBuilder
{
    public static SearchPredicate Build(
        SearchRequest request,
        IReadOnlyList<Guid>? publisherIds = null,
        IReadOnlyList<Guid>? excludePublisherIds = null)
    {
        var clauses = new List<string>();

        // 1. Text — her terim bir OR-grubu (Name/Description/Tags.Value); gürültü Bm25Terms'te elenir.
        foreach (var t in Bm25Terms.Extract(request.Text))
            clauses.Add($"(data.Name:{t} OR data.Description:{t} OR data.Tags.Value:{t})");

        // 2. Publisher — ad handler'da Marten ile id SET'ine çözülür (Contains → çok yayınevi mümkün).
        // Quoted phrase (term() uuid'de tokenize edip 0 döner); çok id → OR-grubu.
        if (publisherIds is { Count: > 0 })
        {
            var terms = publisherIds.Select(id => $"data.PublisherId:\"{id}\"");
            clauses.Add(publisherIds.Count == 1 ? terms.First() : "(" + string.Join(" OR ", terms) + ")");
        }

        // 3. Category — tek leaf CategoryId, quoted.
        if (request.CategoryId is { } cat)
            clauses.Add($"data.CategoryId:\"{cat}\"");

        // 4. Binding — int enum term.
        if (request.Binding is { } binding)
            clauses.Add($"data.Binding:{(int)binding}");

        // 5. Price range — kapalı [min TO max] / açık >= / <=. Invariant '.' ondalık (010).
        AddRange(clauses, "data.Price.Amount", Num(request.PriceMin), Num(request.PriceMax));

        // 6. Page range.
        AddRange(clauses, "data.PageCount", Num(request.PageMin), Num(request.PageMax));

        // 7. Salable — pozitif bool term.
        if (request.OnlySalable)
            clauses.Add("data.IsSalable:true");

        // 8. Discounted — oran > 0.
        if (request.OnlyDiscounted)
            clauses.Add("data.DiscountRate:>0");

        var pdbClause = clauses.Count > 0 ? string.Join(" AND ", clauses) : null;

        // ExcludePublisher → SQL predicate (NOT string query'de bozuk). Her id bir `<> $n` (hepsi AND).
        var sqlFilters = new List<SqlFilter>();
        if (excludePublisherIds is { Count: > 0 })
            foreach (var ex in excludePublisherIds)
                sqlFilters.Add(new SqlFilter("data->>'PublisherId'", "<>", ex.ToString()));

        return new SearchPredicate(pdbClause, sqlFilters);
    }

    private static void AddRange(List<string> clauses, string field, string? min, string? max)
    {
        if (min is not null && max is not null) clauses.Add($"{field}:[{min} TO {max}]");
        else if (min is not null) clauses.Add($"{field}:>={min}");
        else if (max is not null) clauses.Add($"{field}:<={max}");
    }

    private static string? Num(decimal? v) => v?.ToString(CultureInfo.InvariantCulture);
    private static string? Num(int? v) => v?.ToString(CultureInfo.InvariantCulture);
}

// Birleşik arama predicate'i: @@@ clause (pozitif, null=all()) + @@@ dışı SQL predicate'ler.
public sealed record SearchPredicate(string? PdbClause, IReadOnlyList<SqlFilter> SqlFilters);

// @@@ dışı SQL koşulu (bugün yalnız ExcludePublisher). Executor `{Column} {Op} ${n}` kurar, Value param.
public sealed record SqlFilter(string Column, string Op, object Value);
