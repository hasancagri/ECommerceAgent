using System.Text.RegularExpressions;

namespace Storefront.Api.AgentSql;

// R3: {{EMBED:"metin"}} yer-tutucusu — LLM vektör mekaniği görmez; metni yazar, dönüşümü sistem
// yapar. Akış: ikame → bekçi → (geçerse) embedding üretimi → parametre bind. Parametre bind'i
// SQL-injection yüzeyini embedding metnine kapatır. Metin-literal + CAST şart: Weasel/Npgsql
// Pgvector.Vector'ü bind edemiyor (067 canlı bulgu, ToVectorLiteral buradan sürer).
public static class EmbedPlaceholder
{
    public record EmbedSubstitution(string Sql, IReadOnlyList<string> Texts);

    // Kaçışlı tırnak destekli: {{EMBED:"kitap \"adı\" tema"}}. Marker BÜYÜK harf (kontrat).
    private static readonly Regex PlaceholderRegex = new(
        """\{\{EMBED:"((?:[^"\\]|\\.)*)"\}\}""",
        RegexOptions.Compiled);

    private static readonly Regex MarkerLeftoverRegex = new(
        @"\{\{\s*embed", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ResultDomain<EmbedSubstitution> Substitute(string sql)
    {
        var texts = new List<string>();
        var substituted = PlaceholderRegex.Replace(sql, match =>
        {
            var text = match.Groups[1].Value
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
            texts.Add(text);
            return $"CAST(@emb{texts.Count - 1} AS vector)";
        });

        // Regex'e uymayan her {{EMBED kalıntısı bozuk sözdizimidir (tırnaksız/kapanmamış/küçük harf).
        if (MarkerLeftoverRegex.IsMatch(substituted))
            return ResultDomain<EmbedSubstitution>.Error(new MessageItem
            {
                Code = StorefrontResourceConstants.AgentSqlBadEmbedPlaceholder,
                Property = "malformed {{EMBED:\"text\"}} placeholder"
            });

        if (texts.Any(string.IsNullOrWhiteSpace))
            return ResultDomain<EmbedSubstitution>.Error(new MessageItem
            {
                Code = StorefrontResourceConstants.AgentSqlBadEmbedPlaceholder,
                Property = "empty {{EMBED}} text"
            });

        return ResultDomain<EmbedSubstitution>.Ok(new EmbedSubstitution(substituted, texts));
    }

    // pgvector metin formu: "[0.1,0.2,...]" (InvariantCulture şart — 067'den taşındı).
    public static string ToVectorLiteral(ReadOnlySpan<float> vector)
    {
        var parts = new string[vector.Length];
        for (var i = 0; i < vector.Length; i++)
            parts[i] = vector[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"[{string.Join(',', parts)}]";
    }
}
