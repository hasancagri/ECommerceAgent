using Catalog.Api.Domains.Products;

namespace Catalog.Api.Search;

// 027 Dilim-3 (V2 — vektör): "şuna benzer" = çapa kitabın embedding'ine kNN (`<=>` kosinüs). 021 etiket-BM25
// örtüşmesi + boost SÖKÜLDÜ — vektör tür/ton/konuyu zaten kodluyor, ortak etiket şart değil (Tolstoy≈
// Dostoyevski etiketsiz de yakalanır). Hard filtre KORUNUR: çapanın kendi WorkKey baskıları hariç + seviye
// uyum grubu + (varsa) aynı dil. DISTINCT ON WorkKey (çapaya en yakın baskı) + stok-önce→yakınlık sıralama.
// Çapanın embedding'i yoksa anchor CTE boş → sonuç boş (hata değil). Eşik YOK (çapa-temelli hep anlamlı komşu).
public sealed class SimilarBooksQueries(NpgsqlDataSource dataSource, IQuerySession session)
    : IScopedDependency
{
    private const string EmbeddingTable = $"{SchemaConstants.CatalogSchemaName}.product_embedding";
    private const string ProductTable = $"{SchemaConstants.CatalogSchemaName}.mt_doc_product";

    public async Task<List<Product>> FindSimilarAsync(Product target, int count, CancellationToken ct)
    {
        var levels = BookTaxonomy.CompatibleAudienceLevels(target.AudienceLevel).ToArray();
        var args = new List<object>();
        var pTarget = Param(args, target.Id);
        var pWork = Param(args, target.WorkKey);
        var pLevels = Param(args, levels);

        var where = $"p.data->>'WorkKey' <> {pWork} AND p.data->>'AudienceLevel' = ANY({pLevels})";
        if (!string.IsNullOrWhiteSpace(target.Language))
            where += $" AND p.data->>'Language' = {Param(args, target.Language)}";

        // Çapa embedding'i CTE'de; kNN mesafesi `e.embedding <=> a.embedding`. DISTINCT ON WorkKey → eser
        // başına çapaya en yakın baskı; final sıralama stok-önce → yakınlık → popülerlik. Anchor yoksa 0 satır.
        var sql =
            $"WITH anchor AS (SELECT embedding FROM {EmbeddingTable} WHERE product_id = {pTarget}), " +
            "scored AS (SELECT p.id, (e.embedding <=> a.embedding) AS dist, p.data->>'WorkKey' AS workkey, " +
            "(p.data->>'StockCount')::int AS stock, COALESCE((p.data->>'PurchaseCount')::int,0) AS purchases " +
            $"FROM {EmbeddingTable} e JOIN {ProductTable} p ON p.id = e.product_id CROSS JOIN anchor a " +
            $"WHERE {where}), " +
            "ranked AS (SELECT DISTINCT ON (workkey) id, dist, stock, purchases FROM scored ORDER BY workkey, dist ASC) " +
            $"SELECT id FROM ranked ORDER BY (stock > 0) DESC, dist ASC, purchases DESC LIMIT {Param(args, count)}";

        var ids = await ReadIdsAsync(sql, ct, args.ToArray());
        if (ids.Count == 0) return [];

        var byId = (await session.Query<Product>().Where(p => ids.Contains(p.Id)).ToListAsync(ct))
            .ToDictionary(p => p.Id);
        return ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
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
