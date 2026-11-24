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
    // Previous: önceki turun tipli filtresi (bağlam). Doluysa LLM daraltma/sıfırlama kararını verir
    // (çok-turlu refinement: "Dostoyevski kitapları" → "sadece İş Kültür'den" = Dostoyevski ∩ İş Kültür).
    public record Query(string Text, BookSearchFilters? Previous = null);

    public class ExtractIntentHandler(ChatClientFactory clients, ScoutOptions options)
    {
        private static readonly System.Text.Json.JsonSerializerOptions PrevJsonOptions =
            new() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

        // Çıkarma hep bir obje döndürür (hata/boş → çıplak-query fallback) → Result sarması gereksiz;
        // doğrudan BookSearchFilters. (record new() kısıtını karşılamadığı için FeatureObjectResultModel değil.)
        public async Task<BookSearchFilters> Handle(Query query, CancellationToken ct)
        {
            var client = clients.ForModel(ScoutModelResolver.Resolve(options, "catalog"));
            var messages = new List<MeaiChatMessage>
            {
                new(MeaiChatRole.System, ExtractPrompt.Instructions),
            };
            // Bağlam: önceki tipli filtre varsa daraltma/sıfırlama kuralıyla ver.
            if (query.Previous is not null)
            {
                var prevJson = System.Text.Json.JsonSerializer.Serialize(query.Previous, PrevJsonOptions);
                messages.Add(new(MeaiChatRole.System,
                    "ÖNCEKİ ARAMA (tipli): " + prevJson + "\n" +
                    "Yeni kullanıcı mesajı bu aramayı DARALTIYORSA (ör. 'sadece İş Kültür'den', 'ucuz " +
                    "olanları', 'ciltli olanlar') önceki dolu alanları KORU + yeni kısıtı EKLE. TAMAMEN " +
                    "YENİ bir arama ise (farklı yazar/konu, ör. 'Mo Yan'ın kitapları') önceki alanları " +
                    "BIRAK, sıfırdan kur. Kararı sen ver."));
            }
            messages.Add(new(MeaiChatRole.User, query.Text));

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
