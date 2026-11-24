using Microsoft.Extensions.AI;
using ScoutAgent.Api.Agents;
using ScoutAgent.Api.Search;
using MeaiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using MeaiChatRole = Microsoft.Extensions.AI.ChatRole;

namespace ScoutAgent.Api.Domains.Conversations.Features.Queries;

// 022 Tur-1: ham sorgu → TİPLİ niyet (BookSearchFilters). MEAI strict structured output
// (GetResponseAsync<T>) — model yalnız kullanıcının kelimelerini slota koyar (ExtractPrompt). Durumsuz.
// Çıkarma başarısızsa çıplak-query fallback (aramayı bloklamaz; kullanıcı özür görmez). LLM yok Tur-2'de.
public static class ExtractIntent
{
    public record Query(string Text);

    public class ExtractIntentHandler(ChatClientFactory clients, ScoutOptions options)
    {
        // Çıkarma hep bir obje döndürür (hata/boş → çıplak-query fallback) → Result sarması gereksiz;
        // doğrudan BookSearchFilters. (record new() kısıtını karşılamadığı için FeatureObjectResultModel değil.)
        public async Task<BookSearchFilters> Handle(Query query, CancellationToken ct)
        {
            var client = clients.ForModel(ScoutModelResolver.Resolve(options, "catalog"));
            var messages = new List<MeaiChatMessage>
            {
                new(MeaiChatRole.System, ExtractPrompt.Instructions),
                new(MeaiChatRole.User, query.Text),
            };

            try
            {
                var response = await client.GetResponseAsync<BookSearchFilters>(messages, cancellationToken: ct);
                return response.TryGetResult(out var v) && v is not null ? v : Fallback(query.Text);
            }
            catch
            {
                // Model/servis hatası → çıplak query fallback (deterministik arama yine çalışır).
                return Fallback(query.Text);
            }
        }

        // Çıkarma başarısız → tüm sorgu serbest-metin query (Catalog BM25 + facet çıkarımı yine devrede).
        private static BookSearchFilters Fallback(string text) => new(Text: new TextSlot(Query: text));
    }
}
