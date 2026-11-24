using ScoutAgent.Api.Agents;

namespace ScoutAgent.Api.Domains.Conversations.Features.Queries;

// 028 Dilim-4: "sana özel" feed orkestrasyonu (sorgu YOK → kişisel). İki MCP adımı:
// 1) Personalization get_my_taste (PERSONAL — kullanıcı token'ı forward edilir) → taste vektör + signalCount.
// 2) Catalog recommend_books — signalCount<3/boş ise boş taste (küratörlü), değilse taste vektörüyle kNN.
// Vektör yalnız bu orkestrasyonda taşınır; LOGLANMAZ, LLM prompt'una girmez, kullanıcıya dönmez (İlke VIII).
// DETERMİNİSTİK (LLM yok). Endpoint login-zorunlu (anonim 401).
public static class Feed
{
    public record Query();

    public class FeedHandler(McpToolSource mcp)
    {
        private const int ColdStartThreshold = 3;
        private const int FeedCount = 12;

        public async Task<FeatureObjectResultModel<ChatResponse>> Handle(Query query, CancellationToken ct)
        {
            var collector = new ToolResultCollector();

            // 1. Taste (Personalization, personal — token forward). Hata/boş → cold.
            TasteResult? taste = null;
            try
            {
                var pClient = await mcp.GetClientAsync("personalization", ct);
                var tasteCall = await pClient.CallToolAsync(
                    "get_my_taste", new Dictionary<string, object?>(), cancellationToken: ct);
                taste = McpToolSource.DeserializeResult<TasteResult>(tasteCall);
            }
            catch { /* taste alınamadı → küratörlü */ }

            var cold = taste is null || taste.SignalCount < ColdStartThreshold || taste.Vector.Length == 0;

            // 2. Öneri (Catalog): cold → boş taste (küratörlü); değilse taste vektörü. Vektör arg (opak, S2S).
            var cClient = await mcp.GetClientAsync("catalog", ct);
            var recArgs = new Dictionary<string, object?>
            {
                ["taste"] = cold ? Array.Empty<float>() : taste!.Vector,
                ["count"] = FeedCount,
            };
            var recCall = await cClient.CallToolAsync("recommend_books", recArgs, cancellationToken: ct);
            var rec = McpToolSource.DeserializeResult<RecommendBooksResult>(recCall);
            collector.Add("recommend_books", rec, null);

            var total = rec?.Total ?? 0;
            var text = total == 0
                ? "Şu an sana önerecek kitap bulamadım."
                : cold
                    ? "Keşfetmen için öne çıkan kitaplar — aradıkça öneriler sana özelleşecek."
                    : $"Zevkine göre {total} kitap seçtim; aşağıda kartlarda.";

            var reply = new ChatReply
            {
                Text = text,
                Items = collector.Results
                    .Where(r => r.Result is not null)
                    .Select(r => new ChatReplyItem(r.Tool, r.Result!, r.Filters))
                    .ToList(),
            };
            return FeatureObjectResultModel<ChatResponse>.Ok(new ChatResponse { ConversationId = Guid.Empty, Reply = reply });
        }
    }
}
