using System.Globalization;
using System.Text;

namespace Catalog.Api.Search;

// 026: pgvector text-literal `[f1,f2,...]` kurucu (SAF). Vektör param'ı Npgsql vector-plugin'siz yazmak/
// sorgulamak için `$n::vector` CAST'iyle kullanılır (025 upsert + 026 kNN tek kaynak). InvariantGlobalization
// repo-geneli → float ToString hep "." ondalık (virgül tuzağı yok).
public static class VectorLiteral
{
    public static string From(ReadOnlySpan<float> vector)
    {
        var sb = new StringBuilder(vector.Length * 8).Append('[');
        for (var i = 0; i < vector.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(vector[i].ToString(CultureInfo.InvariantCulture));
        }
        return sb.Append(']').ToString();
    }
}
