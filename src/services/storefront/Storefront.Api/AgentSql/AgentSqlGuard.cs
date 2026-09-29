using System.Text;
using System.Text.RegularExpressions;

namespace Storefront.Api.AgentSql;

// R2 katman-1: saf bekçi — çalıştırma ÖNCESİ ucuz ret + makine-okur ret kodu (asistanın düzeltme
// döngüsü, FR-007). Yapısal güvence bekçi DEĞİL kısıtlı roldür (katman-2); bekçi kötücül sorguların
// çoğunu DB'ye hiç göndermez ve log'a NET sebep yazar. Kelime eşleşmesi KELİME-SINIRLI ve
// yorum/literal-DIŞI (OFFSET'teki SET, '%drop%' literal'i tetiklemez — kontrat).
public static class AgentSqlGuard
{
    public record GuardedQuery(string WrappedSql);

    private static readonly string[] AllowedRelations =
    [
        StorefrontSellableSchema.ViewName,
        $"{SchemaConstants.StorefrontSchemaName.ToLowerInvariant()}.{StorefrontSellableSchema.ViewName}"
    ];

    // Kontrat listesi + INTO (SELECT INTO tablo yaratır) + FOR UPDATE/SHARE (satır kilidi).
    private static readonly Regex ForbiddenKeywordRegex = new(
        @"\b(insert|update|delete|drop|alter|create|grant|revoke|copy|truncate|do|execute|merge|call|vacuum|lock|listen|notify|prepare|deallocate|reindex|cluster|comment|refresh|set|into)\b" +
        @"|\bpg_sleep\b|\bpg_read\w*|\bpg_ls_\w+|\bdblink\w*" +
        @"|\bfor\s+(update|share|no\s+key\s+update|key\s+share)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CteNameRegex = new(
        @"\b([a-z_][a-z0-9_]*)\s+as\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TokenRegex = new(
        @"[A-Za-z_][A-Za-z0-9_.]*|\(|\)|,",
        RegexOptions.Compiled);

    // afterRelation durumunu kapatan sözcükler (ORDER BY virgülü yeni-ilişki beklentisi doğurmasın).
    private static readonly HashSet<string> ClauseBreakers =
    [
        "on", "using", "where", "group", "order", "having", "limit", "offset",
        "union", "intersect", "except", "select", "window", "for", "tablesample"
    ];

    public static ResultDomain<GuardedQuery> Validate(string sql, int maxSqlLength, int maxRows)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return Reject(StorefrontResourceConstants.AgentSqlNotReadOnly, "empty sql");

        if (sql.Length > maxSqlLength)
            return Reject(StorefrontResourceConstants.AgentSqlTooLong, $"sql length {sql.Length} > {maxSqlLength}");

        // Yorumlar + string literal'ler denetim metninden soyulur (içerikleri kural tetiklemez).
        var scannable = StripCommentsAndLiterals(sql);

        // Tek statement: sondaki ';' tolere edilir; içeride ';' = ikinci statement.
        var body = scannable.TrimEnd();
        body = body.TrimEnd(';').TrimEnd();
        if (body.Contains(';'))
            return Reject(StorefrontResourceConstants.AgentSqlMultiStatement, "multiple statements");

        var firstWord = Regex.Match(body, @"^\s*([A-Za-z]+)").Groups[1].Value.ToLowerInvariant();
        if (firstWord is not ("select" or "with"))
            return Reject(StorefrontResourceConstants.AgentSqlNotReadOnly, $"statement starts with '{firstWord}'");

        var forbidden = ForbiddenKeywordRegex.Match(body);
        if (forbidden.Success)
            return Reject(StorefrontResourceConstants.AgentSqlForbiddenKeyword, $"forbidden keyword '{forbidden.Value}'");

        var unknownRelation = FindUnknownRelation(body);
        if (unknownRelation is not null)
            return Reject(StorefrontResourceConstants.AgentSqlUnknownRelation,
                $"relation '{unknownRelation}' is not allowed; only '{StorefrontSellableSchema.ViewName}'");

        // R4: tavan sarmalaması — MaxRows+1 döndüyse Truncated (LLM'in kendi LIMIT'i güvence değildir).
        var inner = sql.TrimEnd();
        inner = inner.TrimEnd(';').TrimEnd();
        var wrapped = $"select * from (\n{inner}\n) _guard limit {maxRows + 1}";

        return ResultDomain<GuardedQuery>.Ok(new GuardedQuery(wrapped));
    }

    private static ResultDomain<GuardedQuery> Reject(string code, string detail) =>
        ResultDomain<GuardedQuery>.Error(new MessageItem { Code = code, Property = detail });

    // FROM/JOIN sonrası düz tanımlayıcılar whitelist'te olmalı. İSTİSNALAR (kontrat): '(' = alt-sorgu,
    // 'ad(' = set-returning fonksiyon (unnest/jsonb_array_elements...), CTE alias'ları.
    // Virgüllü cross-join listesi desteklenir (FROM x s, unnest(s.authors) a).
    private static string? FindUnknownRelation(string body)
    {
        var allowed = new HashSet<string>(AllowedRelations, StringComparer.OrdinalIgnoreCase);
        foreach (Match cte in CteNameRegex.Matches(body))
            allowed.Add(cte.Groups[1].Value);

        var tokens = TokenRegex.Matches(body).Select(m => m.Value).ToList();

        var depth = 0;
        var expectRelation = false;
        var afterRelation = false;
        var afterRelationDepth = -1;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var lower = token.ToLowerInvariant();

            switch (token)
            {
                case "(":
                    depth++;
                    expectRelation = false; // alt-sorgu kaynağı
                    continue;
                case ")":
                    depth--;
                    if (afterRelation && depth < afterRelationDepth)
                        afterRelation = false;
                    continue;
                case ",":
                    if (afterRelation && depth == afterRelationDepth)
                        expectRelation = true;
                    continue;
            }

            if (lower is "from" or "join")
            {
                expectRelation = true;
                afterRelation = false;
                continue;
            }

            if (expectRelation && lower is "lateral" or "only")
                continue;

            if (ClauseBreakers.Contains(lower))
            {
                afterRelation = false;
                expectRelation = false;
                continue;
            }

            if (!expectRelation)
                continue;

            // 'ad(' = fonksiyon kaynağı — güvenlik kelime-listesi + rol + timeout'a emanet.
            var isFunctionCall = i + 1 < tokens.Count && tokens[i + 1] == "(";
            if (!isFunctionCall && !allowed.Contains(lower))
                return token;

            expectRelation = false;
            afterRelation = true;
            afterRelationDepth = depth;
        }

        return null;
    }

    // '--' satır yorumu, '/* */' blok yorumu (iç içe), '...' literal ('' kaçışlı) ve $tag$...$tag$
    // dolar-literal'i boşlukla değiştirilir — token sınırları korunur, içerikleri denetime girmez.
    private static string StripCommentsAndLiterals(string sql)
    {
        var result = new StringBuilder(sql.Length);
        var i = 0;

        while (i < sql.Length)
        {
            var c = sql[i];

            if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] != '\n') { result.Append(' '); i++; }
                continue;
            }

            if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                var commentDepth = 0;
                while (i < sql.Length)
                {
                    if (sql[i] == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
                    {
                        commentDepth++;
                        result.Append("  ");
                        i += 2;
                        continue;
                    }
                    if (sql[i] == '*' && i + 1 < sql.Length && sql[i + 1] == '/')
                    {
                        commentDepth--;
                        result.Append("  ");
                        i += 2;
                        if (commentDepth == 0) break;
                        continue;
                    }
                    result.Append(sql[i] == '\n' ? '\n' : ' ');
                    i++;
                }
                continue;
            }

            if (c == '\'')
            {
                result.Append(' ');
                i++;
                while (i < sql.Length)
                {
                    if (sql[i] == '\'')
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == '\'') { result.Append("  "); i += 2; continue; }
                        result.Append(' ');
                        i++;
                        break;
                    }
                    result.Append(' ');
                    i++;
                }
                continue;
            }

            if (c == '$')
            {
                var tagEnd = sql.IndexOf('$', i + 1);
                if (tagEnd > i)
                {
                    var tag = sql.Substring(i, tagEnd - i + 1);
                    if (Regex.IsMatch(tag, @"^\$[A-Za-z_]*\$$"))
                    {
                        var close = sql.IndexOf(tag, tagEnd + 1, StringComparison.Ordinal);
                        var end = close < 0 ? sql.Length : close + tag.Length;
                        result.Append(' ', end - i);
                        i = end;
                        continue;
                    }
                }
            }

            result.Append(c);
            i++;
        }

        return result.ToString();
    }
}
