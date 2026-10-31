using System.Security.Cryptography;
using System.Text;

namespace Catalog.Api.Embeddings;

// 096: embed edilen metnin TEK tanım yeri (FR-003) — bileşim + kırpma + hash saf çekirdeği
// (İlke VI test-first). Satır formatı data-model.md'de sabit; hash KIRPILMIŞ metinden (tutarlılık),
// model adı hash'e girmez (ayrı bayatlık ekseni — ProductEmbedding.Model).
public static class EmbeddingText
{
    /// <summary>Künye metnini birleştirir: ad / "Yazarlar: a, b" / "Kategori: Leaf > Parent" /
    /// açıklama. Boş parça satır üretmez; MaxChars aşımında sondan kırpar.</summary>
    public static string Compose(
        string name,
        string description,
        IReadOnlyList<string> authorNames,
        IReadOnlyList<string> categoryChainNames,
        int maxChars)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(name))
            builder.Append(name.Trim());

        if (authorNames.Count > 0)
            AppendLine(builder, $"Yazarlar: {string.Join(", ", authorNames)}");

        if (categoryChainNames.Count > 0)
            AppendLine(builder, $"Kategori: {string.Join(" > ", categoryChainNames)}");

        if (!string.IsNullOrWhiteSpace(description))
            AppendLine(builder, description.Trim());

        var composed = builder.ToString();
        return composed.Length <= maxChars ? composed : composed[..maxChars];
    }

    /// <summary>SHA256 hex — tazelik anahtarı. Aynı girdi → aynı hash (deterministik).</summary>
    public static string Hash(string composedText)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(composedText)));

    private static void AppendLine(StringBuilder builder, string line)
    {
        if (builder.Length > 0)
            builder.Append('\n');
        builder.Append(line);
    }
}
