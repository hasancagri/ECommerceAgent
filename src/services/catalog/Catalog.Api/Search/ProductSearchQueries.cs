using System.Globalization;
using System.Text;

namespace Catalog.Api.Search;

// 018: ParadeDB ham-SQL birleşik arama yürütücüsü (BM25 `@@@` + `paradedb.score`/fast-field sort).
// TEK yol (SearchAsync): metin/filtre/sort/browse tek SQL'de; sıralı productId → künye aynı BC'den
// Marten'le çözülür (sıra korunur). Marten LINQ bu operatörleri bilmez → kendi NpgsqlDataSource'u.
// (Semantik/kNN/embedding ayağı 018'de SÖKÜLDÜ — ileride yeni spec.)
public sealed class ProductSearchQueries(NpgsqlDataSource dataSource, IQuerySession session)
    : IScopedDependency
{
    private const string ProductTable = $"{SchemaConstants.CatalogSchemaName}.mt_doc_product";

    // 018 T011: TEK birleşik arama yolu — tüm müşteri aramaları (metin/filtre/sort/browse) buraya.
    // SearchPredicateBuilder @@@ clause + SQL predicate kurar, mod ORDER BY'ı seçer; tek SQL filtre+sort+
    // sayfalamayı DB'de yapar (aday yükleme YOK). Total süzülmüş sayıyı yansıtır (sayfalama öncesi).
    public async Task<SearchPage> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        // 1. Publisher ad→id SET (Marten). Ad verilip hiç eşleşme yoksa sonuç boş (dahil süzgeci boş tutar).
        IReadOnlyList<Guid>? includePub = null, excludePub = null;
        if (!string.IsNullOrWhiteSpace(request.Publisher))
        {
            includePub = await ResolvePublisherIdsAsync(request.Publisher.Trim(), ct);
            if (includePub.Count == 0) return SearchPage.Empty;
        }
        if (!string.IsNullOrWhiteSpace(request.ExcludePublisher))
            excludePub = await ResolvePublisherIdsAsync(request.ExcludePublisher.Trim(), ct);

        // 2. Predicate + mod.
        var predicate = SearchPredicateBuilder.Build(request, includePub, excludePub);
        var hasText = Bm25Terms.Extract(request.Text).Count > 0;
        var mode = SearchModeResolver.Resolve(hasText, request.Sort);

        // 3. WHERE + dinamik param'lar. Clause varsa `@@@ $1`, yoksa `paradedb.all()`; SQL predicate'ler AND.
        var args = new List<object>();
        var where = new StringBuilder();
        if (predicate.PdbClause is { } clause)
        {
            args.Add(clause);
            where.Append($"id @@@ ${args.Count}");
        }
        else where.Append("id @@@ paradedb.all()");
        foreach (var f in predicate.SqlFilters)
        {
            args.Add(f.Value);
            where.Append($" AND {f.Column} {f.Op} ${args.Count}");
        }

        // 4. Total (süzülmüş; sayfalama/sıralama öncesi).
        var total = await ScalarLongAsync(
            $"SELECT count(*) FROM {ProductTable} WHERE {where}", ct, args.ToArray());
        if (total == 0) return SearchPage.Empty;

        // 5. Sayfa id'leri — mod ORDER BY + LIMIT/OFFSET.
        var limit = SearchParameters.NormalizeLimit(request.PageSize);
        var offset = (Math.Max(request.Page, 1) - 1) * limit;
        var pageArgs = new List<object>(args) { limit, offset };
        var pageSql = $"SELECT id FROM {ProductTable} WHERE {where} " +
                      $"ORDER BY {OrderByClause(mode, request.Sort)} " +
                      $"LIMIT ${pageArgs.Count - 1} OFFSET ${pageArgs.Count}";
        var ids = await ReadIdsAsync(pageSql, ct, pageArgs.ToArray());

        // 6. Sıralı künye (Marten; DB sırası otoriter).
        var products = await ResolveOrderedAsync(ids, ct);
        return new SearchPage(products, total);
    }

    // Publisher adı → eşleşen id'ler. Token-bazlı: sorgu NameNormalization'la normalize edilir (trim +
    // boşluk-tekle + ToUpperInvariant — Publisher.NormalizedName ile AYNI sözleşme), token'lara bölünür;
    // NormalizedName TÜM token'ları (sırasız) içeren yayınevleri eşleşir. Bitişik substring DEĞİL →
    // "İş Kültür" kısaltması "İş Bankası Kültür Yayınları"yı bulur (araya kelime girse de). Çok eşleşme OK.
    private async Task<IReadOnlyList<Guid>> ResolvePublisherIdsAsync(string name, CancellationToken ct)
    {
        var tokens = NameNormalization.Normalize(name).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return [];
        IQueryable<Publisher> q = session.Query<Publisher>();
        foreach (var token in tokens)
            q = q.Where(p => p.NormalizedName.Contains(token)); // Marten → ILIKE %token%; AND = hepsi şart.
        return await q.Select(p => p.Id).ToListAsync(ct);
    }

    // Mod → ORDER BY. Relevance = BM25 skoru (Top-K pushdown); FieldSort/DefaultBrowse = jsonb fast-field
    // (Normal scan; 22k'da kabul, ≥1M typed-kolon backlog — research.md). `data.X` SQL ORDER BY'da kullanılamaz.
    private static string OrderByClause(SearchMode mode, SortOption? sort) => mode switch
    {
        SearchMode.Relevance => "paradedb.score(id) DESC",
        SearchMode.FieldSort => sort switch
        {
            SortOption.PriceAsc => "(data->'Price'->>'Amount')::numeric ASC NULLS LAST",
            SortOption.PriceDesc => "(data->'Price'->>'Amount')::numeric DESC NULLS LAST",
            SortOption.Newest => "(data->>'PublishedOn')::date DESC NULLS LAST",
            _ => "(data->>'PurchaseCount')::int DESC NULLS LAST", // Popular
        },
        _ => "(data->>'PurchaseCount')::int DESC NULLS LAST", // DefaultBrowse = popülerlik
    };

    private async Task<long> ScalarLongAsync(string sql, CancellationToken ct, params object[] args)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var arg in args)
            cmd.Parameters.Add(new NpgsqlParameter { Value = arg });
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is long l ? l : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private async Task<IReadOnlyList<Guid>> ReadIdsAsync(string sql, CancellationToken ct, params object[] args)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var arg in args)
            cmd.Parameters.Add(new NpgsqlParameter { Value = arg });

        var ids = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetGuid(0));
        return ids;
    }

    // Sıralı productId → ürün künyesi (aynı BC, Marten). Arama SIRASI KORUNUR (DB sırası otoriter).
    public async Task<List<Product>> ResolveOrderedAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var byId = (await session.Query<Product>().Where(p => ids.Contains(p.Id)).ToListAsync(ct))
            .ToDictionary(p => p.Id);
        return ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }
}

// 018 T011: birleşik arama sonucu — sayfa künyeleri (sıralı) + süzülmüş toplam (sayfalama öncesi).
public sealed record SearchPage(IReadOnlyList<Product> Items, long Total)
{
    public static readonly SearchPage Empty = new([], 0);
}
