using System.Globalization;

namespace Catalog.Api.Search;

// 011 T004/T007/T013: ParadeDB ham-SQL arama yürütücüsü. BM25 (@@@ + pdb.score), kNN (<=> kosinüs),
// benzer (ürün vektörü→komşu). Her sorgu SIRALI productId listesi döner; künye aynı BC'den Marten'le
// çözülür (sıra korunur). Marten LINQ bu operatörleri bilmez → kendi NpgsqlDataSource'u.
// FLAG (canlı doğrula): @@@ çok-alan OR skoru, ::pdb.icu, pdb.score vs paradedb.score — sürüme göre.
public sealed class ProductSearchQueries(NpgsqlDataSource dataSource, IQuerySession session)
    : IScopedDependency
{
    private const string ProductTable = $"{SchemaConstants.CatalogSchemaName}.mt_doc_product";
    private const string EmbeddingTable = $"{SchemaConstants.CatalogSchemaName}.product_embedding";

    // US1 — BM25 keyword: tüm `data` JSONB üstünde alaka-sıralı (index tek `data` alanı — ParadeDB
    // çok-expression'ı reddeder). $1=sorgu metni, $2=limit, $3=offset.
    private const string Bm25Sql = $"""
        SELECT id FROM {ProductTable}
        WHERE data @@@ $1
        ORDER BY pdb.score(id) DESC
        LIMIT $2 OFFSET $3
        """;

    // US2 — anlamca ara: sorgu-vektör ($1 = '[...]'::vector) → en yakın komşular (kosinüs).
    private const string SemanticSql = $"""
        SELECT product_id FROM {EmbeddingTable}
        ORDER BY embedding <=> $1::vector
        LIMIT $2 OFFSET $3
        """;

    // US2 — benzer kitaplar: ISBN'in ürün vektörü → komşular (kendini ele). Vektör yoksa boş.
    private const string SimilarSql = $"""
        WITH target AS (
            SELECT e.product_id AS pid, e.embedding AS vec
            FROM {EmbeddingTable} e
            JOIN {ProductTable} p ON p.id = e.product_id
            WHERE p.data->>'Gtin' = $1
            LIMIT 1
        )
        SELECT e.product_id FROM {EmbeddingTable} e, target
        WHERE e.product_id <> target.pid
        ORDER BY e.embedding <=> target.vec
        LIMIT $2
        """;

    public Task<IReadOnlyList<Guid>> Bm25Async(string query, int limit, int offset, CancellationToken ct)
    {
        // ParadeDB `@@@` metni QUERY-PARSER olarak yorumlar — apostrof/`:`/`(`/`"`… özel karakterleri
        // patlatır (500). Kullanıcı serbest metni → yalnız harf/rakam/boşluk bırak (terimler plain OR'lanır).
        var clean = SanitizeBm25(query);
        return clean.Length == 0
            ? Task.FromResult<IReadOnlyList<Guid>>([])
            : ReadIdsAsync(Bm25Sql, ct, clean, limit, offset);
    }

    private static string SanitizeBm25(string query) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(query, @"[^\p{L}\p{N}\s]", " "),
            @"\s+", " ").Trim();

    public Task<IReadOnlyList<Guid>> SemanticAsync(float[] queryVector, int limit, int offset, CancellationToken ct) =>
        ReadIdsAsync(SemanticSql, ct, ToVectorLiteral(queryVector), limit, offset);

    public Task<IReadOnlyList<Guid>> SimilarAsync(string isbn, int limit, CancellationToken ct) =>
        ReadIdsAsync(SimilarSql, ct, isbn, limit);

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

    // pgvector literal: [0.1,0.2,...] — InvariantGlobalization (010) '.' ondalık garantiler.
    private static string ToVectorLiteral(float[] vector) =>
        "[" + string.Join(",", vector.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "]";
}
