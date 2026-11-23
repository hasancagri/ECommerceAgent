using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ScoutAgent.Api.Agents;
using MeaiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using MeaiChatRole = Microsoft.Extensions.AI.ChatRole;

namespace ScoutAgent.Api.Domains.Conversations.Features.Commands;

// 007 US1+US4: bir sohbet turu. Araç kutusu oturum durumuna göre kurulur → yerel model aracı seçer →
// cevap {text, items}. items TOOL DÖNÜŞÜNDEN BİREBİR (uydurma 0). Girişli: kayıt yüklenir/oluşturulur,
// bağlam DB'den kurulur (FR-012 — framework state'ine güvenilmez), kaydedilir (tam transkript).
// Anonim: oturumluk tek mesaj, kalıcı kayıt YOK.
public static class SendMessage
{
    public record Command(Guid? ConversationId, string Message, Guid? UserId,
        IReadOnlyList<HistoryTurn>? History = null);

    // Anon istemci-transkript turu (role: "user"/"assistant"). Login'de yok sayılır — sunucu kaydı otoriter.
    public record HistoryTurn(string Role, string Text);

    // 019 (B): Result TİPLİ — ToolResults ayna-tipi (JsonElement yalnız bilinmeyen tool fallback).
    // Filters: yalnız search_books'ta dolu (kullanılan arama args'ı; React sayfalama için saklar+taşır).
    public record ReplyItem(string Tool, object Result, object? Filters = null);

    public class Reply
    {
        public string Text { get; set; } = string.Empty;
        public IReadOnlyList<ReplyItem> Items { get; set; } = [];
    }

    public class Response
    {
        public Guid ConversationId { get; set; }
        // 014: ilk turda üretilen akıllı başlık (sidebar; anon bunu sessionStorage'a yazar). Yoksa boş.
        public string? Title { get; set; }
        public Reply Reply { get; set; } = new();
    }

    public class SendMessageHandler(
        ScoutAgentFactory agentFactory,
        IDocumentSession session,
        ScoutOptions options)
    {
        // 017: saklanan kart JSON'u canlı endpoint dönüşüyle AYNI şekilde (camelCase) olmalı — React
        // reload'da aynı ReplyItem[] parse'ını kullanır.
        private static readonly System.Text.Json.JsonSerializerOptions ItemsJsonOptions =
            new() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

        public async Task<FeatureObjectResultModel<Response>> Handle(Command command, CancellationToken ct)
        {
            var isAuthenticated = command.UserId is Guid uid && uid != Guid.Empty;

            // Tur boyunca tool çıktılarını yakala (MAF yanıtta surface etmiyor → reply.items buradan).
            // 019: router agent — sub-agent tool'ları lazy; niyete göre yalnız ilgili MCP ucu bağlanır.
            var collector = new ToolResultCollector();
            var agent = agentFactory.CreateRouter(collector);

            // Girişli: kayıt yükle/oluştur (sahiplik kontrolü) + kullanıcı mesajını ekle.
            Conversation? conversation = null;
            var isNewConversation = isAuthenticated ? false : command.ConversationId is null;
            if (isAuthenticated)
            {
                var userId = command.UserId!.Value;
                if (command.ConversationId is Guid existingId)
                {
                    var found = await session.LoadAsync<Conversation>(existingId, ct);
                    // Sahip + mevcutsa sürdür. Bulunamadı/başkasının (ör. anon-arama sonrası login →
                    // React eski random id yolluyor) → HATA VERME, yeni sohbet başlat (kendini düzeltir).
                    if (found is not null && found.UserId == userId)
                    {
                        conversation = found;
                        var added = conversation.AddUserMessage(command.Message);
                        if (!added.IsSuccess)
                            return FeatureObjectResultModel<Response>.Error(added.Messages);
                    }
                }

                if (conversation is null)
                {
                    var started = Conversation.Start(userId, command.Message);
                    if (!started.IsSuccess)
                        return FeatureObjectResultModel<Response>.Error(started.Messages);
                    conversation = started.Data!;
                    isNewConversation = true;
                }
            }

            // Bağlam: girişli → son N kayıttan (FR-012); anonim → istemci-transkript + son mesaj.
            var contextMessages = BuildContext(conversation, command.Message, command.History,
                options.MaxContextMessages, options.MaxMessageLength);
            var runResponse = await agent.RunAsync(contextMessages, cancellationToken: ct);

            var reply = new Reply
            {
                Text = runResponse.Text ?? string.Empty,
                Items = BuildItems(collector),
            };

            // 014: ilk turda AKILLI başlık üret (Claude-tarzı sidebar). Login → Conversation.Title;
            // anon → Response.Title (istemci sessionStorage'a yazar). Üretilemezse başlık boş (React ilk
            // mesaja düşer). Ayrık + hata-yutar — sohbet cevabını etkilemez.
            string? title = null;
            if (isNewConversation)
            {
                try { title = await agentFactory.GenerateTitleAsync(command.Message, reply.Text, ct); }
                catch { /* başlık üretilemedi */ }
            }

            Guid conversationId;
            if (isAuthenticated && conversation is not null)
            {
                // 017: turda gösterilen kartları (reply.Items) mesaja opak JSON olarak sakla →
                // reload'da birebir render. camelCase (canlı endpoint ile aynı şekil; React {tool,result}).
                // Kart yoksa null — bağlama ASLA girmez (BuildContext yalnız Text okur).
                var itemsJson = reply.Items.Count > 0
                    ? System.Text.Json.JsonSerializer.Serialize(reply.Items, ItemsJsonOptions)
                    : null;
                conversation.AddAssistantMessage(reply.Text, itemsJson);
                if (!string.IsNullOrWhiteSpace(title)) conversation.SetTitle(title);
                session.Store(conversation);
                await session.SaveChangesAsync(ct);
                conversationId = conversation.Id;
            }
            else
            {
                conversationId = command.ConversationId ?? Guid.NewGuid();
            }

            return FeatureObjectResultModel<Response>.Ok(new Response
            {
                ConversationId = conversationId,
                Reply = reply,
            });
        }

        private static List<MeaiChatMessage> BuildContext(
            Conversation? conversation, string latestMessage, IReadOnlyList<HistoryTurn>? history,
            int maxMessages, int maxTurnLen)
        {
            // Tam transkript taşınır (başından sonuna) AMA kötüye-kullanım/maliyet guard'ıyla: son
            // `maxMessages` mesaj + tur başına `maxTurnLen` karakter (aşırı uzun sohbet/dev-metin OpenAI'yi
            // şişirmesin). Normal kitap-keşfi sohbeti bu sınırların çok altında.
            static string Clip(string t, int max) => t.Length > max ? t[..max] : t;

            if (conversation is null)
            {
                // Anon: istemci-transkript (son maxMessages tur, tur-başı kırpılı) + bu mesaj.
                var anon = new List<MeaiChatMessage>();
                if (history is { Count: > 0 })
                    foreach (var turn in history.Skip(history.Count > maxMessages ? history.Count - maxMessages : 0))
                    {
                        if (string.IsNullOrWhiteSpace(turn.Text)) continue;
                        var role = string.Equals(turn.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                            ? MeaiChatRole.Assistant : MeaiChatRole.User;
                        anon.Add(new MeaiChatMessage(role, Clip(turn.Text, maxTurnLen)));
                    }
                anon.Add(new MeaiChatMessage(MeaiChatRole.User, Clip(latestMessage, maxTurnLen)));
                return anon;
            }

            // Login: kaydın son `maxMessages` mesajı (en son kullanıcı mesajı yukarıda eklendi).
            return conversation.Messages
                .Skip(conversation.Messages.Count > maxMessages ? conversation.Messages.Count - maxMessages : 0)
                .Select(m => new MeaiChatMessage(
                    m.Role == ValueObjects.ChatRole.Assistant ? MeaiChatRole.Assistant : MeaiChatRole.User,
                    Clip(m.Text, maxTurnLen)))
                .ToList();
        }

        // 019 (B): collector artık TİPLİ nesne tutuyor — sub-agent'ın tool wrapper'ı MCP
        // structuredContent'i `Deserialize<SomutTip>` etti. Burada sniff/unwrap/reflection YOK; doğrudan
        // reply.items'a. camelCase serileştirme (ItemsJsonOptions + endpoint) React şeklini korur.
        private static IReadOnlyList<ReplyItem> BuildItems(ToolResultCollector collector) =>
            collector.Results
                .Where(r => r.Result is not null)
                .Select(r => new ReplyItem(r.Tool, r.Result!, r.Filters))
                .ToList();
    }
}
