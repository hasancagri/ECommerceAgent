using System.Text;
using Microsoft.Extensions.AI;

namespace Catalog.Api.Search;

// 026 Dilim-2: semantik arama ham-SQL yürütücü (ProductSearchQueries deseni). Sorgu OpenAI'la embed
// (EmbeddingClientFactory, 025 — omurgayla AYNI model/uzay şart) → product_embedding üstünde kNN
// (`<=>` kosinüs) + eşik (MaxCosineDistance altı; alakasız doldurma yok) → mesafe artan sırala → LIMIT.
// US3: yapısal jsonb pre-filter (stokta/indirimli/fiyat/sayfa) mt_doc_product JOIN'iyle — filtre=uygunluk,
// vektör=yakınlık (rol-ayrımı; BM25 metin KARIŞMAZ). Yayınevi/yazar semantik yolda YOK (SemanticIntent demiri).
public sealed class SemanticSearchQueries(
    NpgsqlDataSource dataSource,
    IQuerySession session,
    EmbeddingClientFactory embeddings,
    SemanticSearchOptions options) : IScopedDependency
{
    private const string EmbeddingTable = $"{SchemaConstants.CatalogSchemaName}.product_embedding";
    private const string ProductTable = $"{SchemaConstants.CatalogSchemaName}.mt_doc_product";

    public async Task<List<Product>> SearchAsync(string query, int count, SemanticFilter filter, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var clamped = options.ClampCount(count);
        var generated = await embeddings.Generator.GenerateAsync([query], cancellationToken: ct);
        var vec = VectorLiteral.From(generated[0].Vector.Span);

        // $1=sorgu vektörü (text→vector CAST, WHERE+ORDER BY'da), $2=eşik; dinamik jsonb filtreler sonra.
        var args = new List<object>();
        var pVec = Param(args, vec);
        var pThr = Param(args, options.MaxCosineDistance);
        var where = new StringBuilder($"(e.embedding <=> {pVec}::vector) < {pThr}");
        AppendFilters(where, filter, args);

        var sql =
            $"SELECT e.product_id FROM {EmbeddingTable} e " +
            $"JOIN {ProductTable} p ON p.id = e.product_id " +
            $"WHERE {where} ORDER BY e.embedding <=> {pVec}::vector LIMIT {Param(args, clamped)}";

        var ids = await ReadIdsAsync(sql, ct, args.ToArray());
        if (ids.Count == 0) return [];

        var byId = (await session.Query<Product>().Where(p => ids.Contains(p.Id)).ToListAsync(ct))
            .ToDictionary(p => p.Id);
        return ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }

    // Yapısal jsonb pre-filter (search_books SQL predicate'leriyle aynı anlam). Stok kopyası StockCount,
    // indirim kopyası DiscountRate (003), fiyat Price.Amount, sayfa PageCount. Verilmeyen kısıt atlanır.
    private static void AppendFilters(StringBuilder where, SemanticFilter f, List<object> args)
    {
        if (f.OnlyInStock) where.Append(" AND (p.data->>'StockCount')::int > 0");
        if (f.OnlyDiscounted) where.Append(" AND (p.data->>'DiscountRate') IS NOT NULL");
        if (f.MinPrice is { } min) where.Append($" AND (p.data->'Price'->>'Amount')::numeric >= {Param(args, min)}");
        if (f.MaxPrice is { } max) where.Append($" AND (p.data->'Price'->>'Amount')::numeric <= {Param(args, max)}");
        if (f.MinPageCount is { } pmin) where.Append($" AND (p.data->>'PageCount')::int >= {Param(args, pmin)}");
        if (f.MaxPageCount is { } pmax) where.Append($" AND (p.data->>'PageCount')::int <= {Param(args, pmax)}");
    }

    private static string Param(List<object> args, object value)
    {
        args.Add(value);
        return $"${args.Count}";
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
}

// 026 US3: semantik aramada yapısal kısıt (Flags + fiyat/sayfa aralığı). Yayınevi/yazar YOK (demir →
// SemanticIntent lexical'e yönlendirir). ScoutAgent BookSearchFilters'tan FilterInterpreter'la türetir.
public sealed record SemanticFilter(
    bool OnlyInStock = false, bool OnlyDiscounted = false,
    decimal? MinPrice = null, decimal? MaxPrice = null,
    int? MinPageCount = null, int? MaxPageCount = null)
{
    public static readonly SemanticFilter None = new();
}
