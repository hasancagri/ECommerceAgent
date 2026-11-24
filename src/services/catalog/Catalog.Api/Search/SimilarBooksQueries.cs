using System.Globalization;
using System.Text;
using Catalog.Api.Domains.Products;
using Catalog.Api.Domains.Products.Features.Agents.Queries;

namespace Catalog.Api.Search;

// 021: similar_books ham-SQL yürütücüsü (ParadeDB @@@ + paradedb.score + jsonb hard filtre). Konu/ton
// örtüşmesi BM25'le IDF-ağırlıklı skorlanır (nadir ortak değer güçlü sinyal) + objektif boost (yazar/
// çevirmen/tür); hard filtre dil + seviye uyum grubu + WorkKey (kendi baskıları hariç); DISTINCT ON
// WorkKey eser başına en iyi baskı; sıralama stok-önce → skor → popülerlik. Hedefin örtüşme sinyali
// yoksa (konu+ton boş/belirsiz) fallback: aynı yazar VEYA aynı tür jsonb filtresi (yalnız boost skoru).
// Marten LINQ bu operatörleri bilmez → kendi NpgsqlDataSource'u (ProductSearchQueries deseni). SQL
// host-gerekli → canlı doğrulanır (saf-birim kısmı SimilarBooks.Overlap'te, İlke VI dışı).
public sealed class SimilarBooksQueries(
    NpgsqlDataSource dataSource, IQuerySession session, SimilarBooksOptions options)
    : IScopedDependency
{
    private const string Table = $"{SchemaConstants.CatalogSchemaName}.mt_doc_product";

    public async Task<List<Product>> FindSimilarAsync(Product target, int count, CancellationToken ct)
    {
        var authorArr = target.AuthorIds.Select(a => a.ToString()).ToArray();
        var genreIsKnown = target.Genre != BookTaxonomy.Unknown;
        var bm25 = SimilarBooks.Overlap.BuildBm25Query(target.Themes, target.Moods);

        // Hiçbir sinyal yok (örtüşme + yazar + tür) → anlamlı benzer yok (her şeyi döndürmekten iyidir).
        if (bm25 is null && authorArr.Length == 0 && !genreIsKnown)
            return [];

        var levels = BookTaxonomy.CompatibleAudienceLevels(target.AudienceLevel).ToArray();
        var args = new List<object>();
        var sql = bm25 is not null
            ? BuildOverlapSql(target, authorArr, genreIsKnown, bm25, levels, count, args)
            : BuildFallbackSql(target, authorArr, genreIsKnown, levels, count, args);

        var ids = await ReadIdsAsync(sql, ct, args.ToArray());
        // Sıralı productId → künye (aynı BC, Marten; SQL sırası otoriter — ProductSearchQueries deseni).
        if (ids.Count == 0) return [];
        var byId = (await session.Query<Product>().Where(p => ids.Contains(p.Id)).ToListAsync(ct))
            .ToDictionary(p => p.Id);
        return ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }

    // Örtüşme yolu: BM25 skoru + boost; hard filtre (WorkKey/seviye/dil). paradedb.score @@@ ile AYNI
    // SELECT seviyesinde (CTE sınırı). Boost katsayıları config (Options) — literal gömülür (kullanıcı değil).
    private string BuildOverlapSql(Product target, string[] authorArr, bool genreIsKnown,
        string bm25, string[] levels, int count, List<object> args)
    {
        var score = new StringBuilder("paradedb.score(p.id)");
        AppendBoosts(score, target, authorArr, genreIsKnown, args);

        var pBm25 = Param(args, bm25);
        var where = new StringBuilder($"p.id @@@ {pBm25}");
        AppendHardFilters(where, target, levels, args);

        return Wrap(score.ToString(), where.ToString(), count, args);
    }

    // Fallback: örtüşme yok → "aynı yazar VEYA aynı tür" jsonb filtresi, skor yalnız boost.
    private string BuildFallbackSql(Product target, string[] authorArr, bool genreIsKnown,
        string[] levels, int count, List<object> args)
    {
        var score = new StringBuilder("0");
        AppendBoosts(score, target, authorArr, genreIsKnown, args);

        var sim = new List<string>();
        if (authorArr.Length > 0) sim.Add($"p.data->'AuthorIds'->'$values' ?| {Param(args, authorArr)}");
        if (genreIsKnown) sim.Add($"p.data->>'Genre' = {Param(args, target.Genre)}");
        var where = new StringBuilder("(" + string.Join(" OR ", sim) + ")");
        AppendHardFilters(where, target, levels, args);

        return Wrap(score.ToString(), where.ToString(), count, args);
    }

    private void AppendBoosts(StringBuilder score, Product target, string[] authorArr, bool genreIsKnown,
        List<object> args)
    {
        // TUZAK: liste alanı `->'$values'` (Marten/Newtonsoft `{$type,$values}` sarmalar).
        if (authorArr.Length > 0)
            score.Append($" + CASE WHEN p.data->'AuthorIds'->'$values' ?| {Param(args, authorArr)} THEN {Lit(options.AuthorBoost)} ELSE 0 END");
        if (target.Translators.Count > 0)
            score.Append($" + CASE WHEN p.data->'Translators'->'$values' ?| {Param(args, target.Translators.ToArray())} THEN {Lit(options.TranslatorBoost)} ELSE 0 END");
        if (genreIsKnown)
            score.Append($" + CASE WHEN p.data->>'Genre' = {Param(args, target.Genre)} THEN {Lit(options.GenreBoost)} ELSE 0 END");
    }

    // Hard filtre: kendi baskıları hariç (WorkKey) + seviye uyum grubu + (varsa) aynı dil.
    private static void AppendHardFilters(StringBuilder where, Product target, string[] levels, List<object> args)
    {
        where.Append($" AND p.data->>'WorkKey' <> {Param(args, target.WorkKey)}");
        where.Append($" AND p.data->>'AudienceLevel' = ANY({Param(args, levels)})");
        if (!string.IsNullOrWhiteSpace(target.Language))
            where.Append($" AND p.data->>'Language' = {Param(args, target.Language)}");
    }

    // Ortak sarmalayıcı: skor + WHERE → DISTINCT ON WorkKey (eser başına en iyi) → stok-önce sıralama.
    private static string Wrap(string score, string where, int count, List<object> args) =>
        $"WITH scored AS (SELECT p.id, ({score}) AS score, p.data->>'WorkKey' AS workkey, " +
        $"(p.data->>'StockCount')::int AS stock, COALESCE((p.data->>'PurchaseCount')::int,0) AS purchases " +
        $"FROM {Table} p WHERE {where}), " +
        "ranked AS (SELECT DISTINCT ON (workkey) id, score, stock, purchases FROM scored ORDER BY workkey, score DESC) " +
        $"SELECT id FROM ranked ORDER BY (stock > 0) DESC, score DESC, purchases DESC LIMIT {Param(args, count)}";

    private static string Param(List<object> args, object value)
    {
        args.Add(value);
        return $"${args.Count}";
    }

    private static string Lit(double boost) => boost.ToString(CultureInfo.InvariantCulture);

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
