using Microsoft.Extensions.AI;

namespace Catalog.Api.Search;

// 026 Dilim-2: semantik arama ham-SQL yürütücü (ProductSearchQueries deseni). Sorgu OpenAI'la embed
// (EmbeddingClientFactory, 025 — omurgayla AYNI model/uzay şart) → product_embedding üstünde kNN
// (`<=>` kosinüs) + eşik (MaxCosineDistance altı; alakasız doldurma yok) → mesafe artan sırala → LIMIT.
// Sıralı id → künye (Marten, sıra korunur). Rol-ayrımı: BM25 metin KARIŞMAZ. US3'te jsonb pre-filter eklenir.
public sealed class SemanticSearchQueries(
    NpgsqlDataSource dataSource,
    IQuerySession session,
    EmbeddingClientFactory embeddings,
    SemanticSearchOptions options) : IScopedDependency
{
    private const string EmbeddingTable = $"{SchemaConstants.CatalogSchemaName}.product_embedding";

    public async Task<List<Product>> SearchAsync(string query, int count, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var clamped = options.ClampCount(count);
        var generated = await embeddings.Generator.GenerateAsync([query], cancellationToken: ct);
        var vec = VectorLiteral.From(generated[0].Vector.Span);

        // kNN + eşik: $1=sorgu vektörü (text→vector CAST), $2=eşik, $3=limit. HNSW kosinüs index kullanılır.
        var sql =
            $"SELECT e.product_id FROM {EmbeddingTable} e " +
            "WHERE (e.embedding <=> $1::vector) < $2 " +
            "ORDER BY e.embedding <=> $1::vector LIMIT $3";

        var ids = await ReadIdsAsync(sql, ct, vec, options.MaxCosineDistance, clamped);
        if (ids.Count == 0) return [];

        var byId = (await session.Query<Product>().Where(p => ids.Contains(p.Id)).ToListAsync(ct))
            .ToDictionary(p => p.Id);
        return ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
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
