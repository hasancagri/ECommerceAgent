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

        // 1. Text — her terim bir OR-grubu (Name/Description + taksonomi alanları); gürültü Bm25Terms'te
        // elenir. 021: taksonomi değerleri serbest metin aramasına girer (Tags.Value yerine) — "mizahi"
        // yazan kullanıcı ton'u mizahi kitaplara da ulaşır (açıklamada kelime olmasa bile).
        foreach (var t in Bm25Terms.Extract(request.Text))
            clauses.Add($"(data.Name:{t} OR data.Description:{t} OR data.Genre:{t} " +
                        $"OR data.Moods:{t} OR data.Themes:{t})");

        // 2. Publisher — ad handler'da Marten ile id SET'ine çözülür (Contains → çok yayınevi mümkün).
        // Quoted phrase (term() uuid'de tokenize edip 0 döner); çok id → OR-grubu.
        if (publisherIds is { Count: > 0 })
        {
            var terms = publisherIds.Select(id => $"data.PublisherId:\"{id}\"");
            clauses.Add(publisherIds.Count == 1 ? terms.First() : "(" + string.Join(" OR ", terms) + ")");
        }

        // 3. Binding — int enum term. (021: kategori filtresi SÖKÜLDÜ; taksonomi facet'leri Faz 6.)
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

        // @@@ DIŞI SQL predicate'ler (parametreli; research R1). Executor `{Column} {Op} (${n})` kurar.
        var sqlFilters = new List<SqlFilter>();

        // ExcludePublisher → SQL predicate (NOT string query'de bozuk). Her id bir `<> $n` (hepsi AND).
        if (excludePublisherIds is { Count: > 0 })
            foreach (var ex in excludePublisherIds)
                sqlFilters.Add(new SqlFilter("data->>'PublisherId'", "<>", ex.ToString()));

        // 021: taksonomi facet'leri jsonb operatörüyle (@@@ DIŞI — çok-kelimeli sözlük değerinde token
        // çarpışması false-positive üretir). Tekil alan `= ANY(text[])`; liste alan `?| text[]` (OR hazır).
        AddAnyFilter(sqlFilters, "data->>'Genre'", request.Genres);
        AddAnyFilter(sqlFilters, "data->>'AudienceLevel'", request.AudienceLevels);
        AddExistsFilter(sqlFilters, "data->'Moods'", request.Moods);
        AddExistsFilter(sqlFilters, "data->'Themes'", request.Themes);

        // OnlyInStock — Product.InStock HESAPLANAN (saklanmaz); stok kopyası StockCount>0 süzgeci.
        if (request.OnlyInStock)
            sqlFilters.Add(new SqlFilter("(data->>'StockCount')::int", ">", 0));

        return new SearchPredicate(pdbClause, sqlFilters);
    }

    // Tekil string alan çoklu değer: `data->>'X' = ANY($n)` (değer dizisi text[]).
    private static void AddAnyFilter(List<SqlFilter> filters, string column, IReadOnlyList<string>? values)
    {
        if (values is { Count: > 0 })
            filters.Add(new SqlFilter(column, "= ANY", values.ToArray()));
    }

    // JSON dizi alan çoklu değer: `data->'X' ?| $n` (herhangi biri eleman olarak var mı; text[]).
    private static void AddExistsFilter(List<SqlFilter> filters, string column, IReadOnlyList<string>? values)
    {
        if (values is { Count: > 0 })
            filters.Add(new SqlFilter(column, "?|", values.ToArray()));
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
