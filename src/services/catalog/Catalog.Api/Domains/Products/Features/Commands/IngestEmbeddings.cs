using Catalog.Api.Search;
using Catalog.Api.Search.Embedding;

namespace Catalog.Api.Domains.Products.Features.Commands;

// 011 US2 T012: offline araç çıktısını (ISBN→1536-vektör) product_embedding'e YÜKLER (admin-anahtar
// korumalı). Gtin→product_id çözülür; upsert (ON CONFLICT DO UPDATE). Boyut≠1536 → o kayıt reddedilir
// (iş durmaz); bilinmeyen ISBN atlanır (FR-008 kısmi-kapsam güvenli). Marten LINQ dışı → ham Npgsql.
public static class IngestEmbeddings
{
    public record EmbeddingItem(string Isbn, float[] Vector);

    public record IngestEmbeddingsCommand(IReadOnlyList<EmbeddingItem> Items);

    public class IngestEmbeddingsResponse
    {
        public int Upserted { get; set; }
        public int SkippedUnknownIsbn { get; set; }
        public int RejectedBadDimension { get; set; }
    }

    private const string UpsertSql = $"""
        INSERT INTO {SchemaConstants.CatalogSchemaName}.product_embedding (product_id, embedding)
        SELECT id, $2::vector FROM {SchemaConstants.CatalogSchemaName}.mt_doc_product
        WHERE data->>'Gtin' = $1
        ON CONFLICT (product_id) DO UPDATE SET embedding = EXCLUDED.embedding
        """;

    public class IngestEmbeddingsCommandHandler(NpgsqlDataSource dataSource)
    {
        public async Task<FeatureObjectResultModel<IngestEmbeddingsResponse>> Handle(
            IngestEmbeddingsCommand command, CancellationToken ct)
        {
            var result = new IngestEmbeddingsResponse();
            await using var conn = await dataSource.OpenConnectionAsync(ct);

            foreach (var item in command.Items)
            {
                if (item.Vector.Length != OpenAiEmbeddingOptions.Dimensions)
                {
                    result.RejectedBadDimension++;
                    continue;
                }

                await using var cmd = new NpgsqlCommand(UpsertSql, conn);
                cmd.Parameters.Add(new NpgsqlParameter { Value = item.Isbn });
                cmd.Parameters.Add(new NpgsqlParameter { Value = ToVectorLiteral(item.Vector) });
                var affected = await cmd.ExecuteNonQueryAsync(ct);

                if (affected > 0) result.Upserted++;
                else result.SkippedUnknownIsbn++;
            }

            return FeatureObjectResultModel<IngestEmbeddingsResponse>.Ok(result);
        }
    }

    private static string ToVectorLiteral(float[] vector) =>
        "[" + string.Join(",", vector.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";
}
