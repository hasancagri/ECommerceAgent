using Catalog.Api.Domains.Products;

namespace Catalog.Api.Search;

// 028 Dilim-4: kişisel öneri ham-SQL. Verilen TASTE vektörüyle `product_embedding` kNN (026 deseni) +
// hard filtre (satılabilir: fiyat>0 ∧ stok>0) + `DISTINCT ON WorkKey` (eser başına 1, çeşitlilik) →
// mesafe sırası. Taste BOŞ (cold-start/sinyal yok) → **küratörlü**: satılabilir, popülerlik sıralı
// (018 browse ruhu). Taste vektör ScoutAgent'tan arg olarak gelir (Personalization taste; m2m yok).
public sealed class RecommendQueries(NpgsqlDataSource dataSource, IQuerySession session) : IScopedDependency
{
    private const string EmbeddingTable = $"{SchemaConstants.CatalogSchemaName}.product_embedding";
    private const string ProductTable = $"{SchemaConstants.CatalogSchemaName}.mt_doc_product";
    // Satılabilir proxy: fiyatı var (>0) ∧ stokta. (IsSalable'ın yazar bacağı — çoğu kitapta var; feed için yeter.)
    private const string Salable = "(p.data->'Price'->>'Amount')::numeric > 0 AND (p.data->>'StockCount')::int > 0";

    public async Task<List<Product>> RecommendAsync(float[] taste, int count, CancellationToken ct)
    {
        var limit = count is < 1 or > 20 ? 10 : count;
        var sql = taste.Length == 0 ? CuratedSql(limit) : KnnSql(limit);
        var args = taste.Length == 0
            ? new object[] { limit }
            : [VectorLiteral.From(taste), limit];

        var ids = await ReadIdsAsync(sql, ct, args);
        if (ids.Count == 0) return [];
        var byId = (await session.Query<Product>().Where(p => ids.Contains(p.Id)).ToListAsync(ct))
            .ToDictionary(p => p.Id);
        return ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }

    // kNN: $1=taste vektör, $2=limit. DISTINCT ON WorkKey (çeşitlilik) → mesafe artan.
    private static string KnnSql(int _) =>
        $"WITH scored AS (SELECT p.id, (e.embedding <=> $1::vector) AS dist, p.data->>'WorkKey' AS workkey " +
        $"FROM {EmbeddingTable} e JOIN {ProductTable} p ON p.id = e.product_id WHERE {Salable}), " +
        "ranked AS (SELECT DISTINCT ON (workkey) id, dist FROM scored ORDER BY workkey, dist ASC) " +
        "SELECT id FROM ranked ORDER BY dist ASC LIMIT $2";

    // Küratörlü (cold): satılabilir, popülerlik sıralı, eser başına 1. $1=limit.
    private static string CuratedSql(int _) =>
        $"WITH s AS (SELECT p.id, p.data->>'WorkKey' AS workkey, COALESCE((p.data->>'PurchaseCount')::int,0) AS pop " +
        $"FROM {ProductTable} p WHERE {Salable}), " +
        "ranked AS (SELECT DISTINCT ON (workkey) id, pop FROM s ORDER BY workkey, pop DESC) " +
        "SELECT id FROM ranked ORDER BY pop DESC LIMIT $1";

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
