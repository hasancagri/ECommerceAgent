using Marten;
using Microsoft.Extensions.AI;
using Personalization.Api.Domains.SearchHistory;
using Personalization.Api.Search;

namespace Personalization.Api.Domains.Taste.Features.Queries;

// 028 Dilim-4: kullanıcının zevk-vektörünü ON-DEMAND hesaplar — `UserSearchRecord` distinct sorgu metinleri
// (taze-önce, en çok 200) → OpenAI embed → `TasteVector.Average` merkez. signalCount = distinct sorgu sayısı
// (cold-start kararı). Arama-only sinyal (satın-alma/yorum ağırlıklı kablo TasteVector.Average'da hazır, 0 veri).
// Boş geçmiş/boş userId → boş vektör + 0 (ScoutAgent küratörlüye düşer). Handler deps = Handle param (Wolverine).
public static class ComputeTaste
{
    public record ComputeTasteQuery(Guid UserId);

    // class (FeatureObjectResultModel<T> new() ister — 013 SearchHistoryResponse deseni).
    public class TasteResponse
    {
        public float[] Vector { get; set; } = [];
        public int SignalCount { get; set; }
    }

    public class Handler
    {
        public async Task<FeatureObjectResultModel<TasteResponse>> Handle(
            ComputeTasteQuery query, IQuerySession session, EmbeddingClientFactory embeddings, CancellationToken ct)
        {
            if (query.UserId == Guid.Empty)
                return FeatureObjectResultModel<TasteResponse>.Ok(new TasteResponse());

            var records = await session.Query<UserSearchRecord>()
                .Where(r => r.UserId == query.UserId)
                .OrderByDescending(r => r.SearchedAt)
                .Take(200)
                .ToListAsync(ct);

            var queries = records
                .Select(r => r.Query?.Trim())
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .Select(q => q!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (queries.Count == 0)
                return FeatureObjectResultModel<TasteResponse>.Ok(new TasteResponse());

            var embs = await embeddings.Generator.GenerateAsync(queries, cancellationToken: ct);
            var vectors = embs.Select(e => e.Vector.ToArray()).ToList();
            return FeatureObjectResultModel<TasteResponse>.Ok(
                new TasteResponse { Vector = TasteVector.Average(vectors), SignalCount = queries.Count });
        }
    }
}
