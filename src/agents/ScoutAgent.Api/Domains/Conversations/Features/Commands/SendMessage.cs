using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ScoutAgent.Api.Agents;
using MeaiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using MeaiChatRole = Microsoft.Extensions.AI.ChatRole;

namespace ScoutAgent.Api.Domains.Conversations.Features.Commands;

// 007 US1+US4: bir sohbet turu. Araç kutusu oturum durumuna göre kurulur → yerel model aracı seçer →
// cevap {text, items}. items TOOL DÖNÜŞÜNDEN BİREBİR (uydurma 0). Girişli: kayıt yüklenir/oluşturulur,
// bağlam DB'den kurulur (FR-012 — framework state'ine güvenilmez), kaydedilir, eşik aşılınca özetlenir.
// Anonim: oturumluk tek mesaj, kalıcı kayıt YOK.
public static class SendMessage
{
    public record Command(Guid? ConversationId, string Message, Guid? UserId,
        IReadOnlyList<HistoryTurn>? History = null);

    // Anon istemci-transkript turu (role: "user"/"assistant"). Login'de yok sayılır — sunucu kaydı otoriter.
    public record HistoryTurn(string Role, string Text);

    public record ReplyItem(string Tool, System.Text.Json.JsonElement Result);

    public class Reply
    {
        public string Text { get; set; } = string.Empty;
        public IReadOnlyList<ReplyItem> Items { get; set; } = [];
    }

    public class Response
    {
        public Guid ConversationId { get; set; }
        public Reply Reply { get; set; } = new();
    }

    public class SendMessageHandler(
        ScoutAgentFactory agentFactory,
        McpToolSource toolSource,
        IDocumentSession session,
        ScoutOptions options)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Command command, CancellationToken ct)
        {
            var isAuthenticated = command.UserId is Guid uid && uid != Guid.Empty;

            // Tur boyunca tool çıktılarını yakala (MAF yanıtta surface etmiyor → reply.items buradan).
            var collector = new ToolResultCollector();
            var tools = await toolSource.GetToolsAsync(isAuthenticated, collector, ct);
            var agent = agentFactory.CreateAgent(tools);

            // Girişli: kayıt yükle/oluştur (sahiplik kontrolü) + kullanıcı mesajını ekle.
            Conversation? conversation = null;
            if (isAuthenticated)
            {
                var userId = command.UserId!.Value;
                if (command.ConversationId is Guid existingId)
                {
                    conversation = await session.LoadAsync<Conversation>(existingId, ct);
                    if (conversation is null || conversation.UserId != userId)
                        return FeatureObjectResultModel<Response>.NotFound();

                    var added = conversation.AddUserMessage(command.Message);
                    if (!added.IsSuccess)
                        return FeatureObjectResultModel<Response>.Error(added.Messages);
                }
                else
                {
                    var started = Conversation.Start(userId, command.Message);
                    if (!started.IsSuccess)
                        return FeatureObjectResultModel<Response>.Error(started.Messages);
                    conversation = started.Data!;
                }
            }

            // Bağlam: girişli → [özet]+[son N] kayıttan (FR-012); anonim → istemci-transkript + son mesaj.
            var contextMessages = BuildContext(conversation, command.Message, command.History, options.WindowSize);
            var runResponse = await agent.RunAsync(contextMessages, cancellationToken: ct);

            var reply = new Reply
            {
                Text = runResponse.Text ?? string.Empty,
                Items = BuildItems(collector),
            };

            Guid conversationId;
            if (isAuthenticated && conversation is not null)
            {
                conversation.AddAssistantMessage(reply.Text);
                await MaybeSummarizeAsync(conversation, ct);
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
            Conversation? conversation, string latestMessage, IReadOnlyList<HistoryTurn>? history, int windowSize)
        {
            if (conversation is null)
            {
                // Anon: istemci-transkript (son N tur) + bu mesaj. Devamlılık/sayfalama bağlamı burada taşınır
                // (sunucuda anon kaydı yok). Boş/eksik history → yalnız tek mesaj (eski davranış).
                var anon = new List<MeaiChatMessage>();
                if (history is { Count: > 0 })
                {
                    var window = windowSize < 0 ? 0 : windowSize;
                    foreach (var turn in history.Skip(history.Count > window ? history.Count - window : 0))
                    {
                        if (string.IsNullOrWhiteSpace(turn.Text)) continue;
                        var role = string.Equals(turn.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                            ? MeaiChatRole.Assistant : MeaiChatRole.User;
                        anon.Add(new MeaiChatMessage(role, turn.Text));
                    }
                }
                anon.Add(new MeaiChatMessage(MeaiChatRole.User, latestMessage));
                return anon;
            }

            // conversation.Messages en son kullanıcı mesajını zaten içerir (yukarıda eklendi).
            var context = ConversationContext.Build(conversation.Summary, conversation.Messages, windowSize);
            return context.Select(m => new MeaiChatMessage(ToRole(m.Role), m.Text)).ToList();
        }

        private static MeaiChatRole ToRole(ContextRole role) => role switch
        {
            ContextRole.System => MeaiChatRole.System,
            ContextRole.Assistant => MeaiChatRole.Assistant,
            _ => MeaiChatRole.User,
        };

        // Eşik aşılınca pencere dışındaki eskileri özetle (bağlam maliyeti sabit — FR-011/SC-005).
        private async Task MaybeSummarizeAsync(Conversation conversation, CancellationToken ct)
        {
            var unsummarized = conversation.Messages.Count - conversation.SummarizedUpTo;
            if (unsummarized <= options.SummaryThreshold)
                return;

            var upTo = conversation.Messages.Count - options.WindowSize;
            if (upTo <= conversation.SummarizedUpTo)
                return;

            var slice = conversation.Messages.Take(upTo).Skip(conversation.SummarizedUpTo);
            var transcript = string.Join("\n", slice.Select(m => $"{m.Role}: {m.Text}"));
            var existing = string.IsNullOrWhiteSpace(conversation.Summary)
                ? string.Empty
                : $"Önceki özet: {conversation.Summary}\n\n";

            var summary = await agentFactory.SummarizeAsync(existing + transcript, ct);
            if (!string.IsNullOrWhiteSpace(summary))
                conversation.ApplySummary(summary, upTo);
        }

        // Yakalanan tool çıktılarını reply.items'a çevirir (collector'dan — MAF yanıtına bağlı değil).
        // MCP sonucu string(JSON)/JsonElement/obje olabilir → normalize edip JsonElement döndür.
        private static IReadOnlyList<ReplyItem> BuildItems(ToolResultCollector collector) =>
            collector.Results
                .Where(r => r.Result is not null)
                .Select(r => new ReplyItem(r.Tool, ToElement(r.Result!)))
                .ToList();

        private static System.Text.Json.JsonElement ToElement(object result)
        {
            // 1) Ham sonucu JsonElement'e getir.
            System.Text.Json.JsonElement el;
            switch (result)
            {
                case System.Text.Json.JsonElement je:
                    el = je; break;
                case string s:
                    var t = s.TrimStart();
                    if ((t.StartsWith('{') || t.StartsWith('[')))
                    {
                        try { return Unwrap(System.Text.Json.JsonDocument.Parse(s).RootElement.Clone()); }
                        catch { return System.Text.Json.JsonSerializer.SerializeToElement(new { text = s }); }
                    }
                    return System.Text.Json.JsonSerializer.SerializeToElement(new { text = s });
                default:
                    el = System.Text.Json.JsonSerializer.SerializeToElement(result); break;
            }
            return Unwrap(el);
        }

        // MCP sonucu birçok şekle sarmalanabilir (sürüm/transport'a göre): `CallToolResult`
        // (structuredContent / content[].text), ya da AIContent TextContent (`{"$type":"text","Text":"<json>"}`),
        // ya da bunların dizisi. Hepsini açıp iç JSON'u (React'ın beklediği top-level `items`) döndürür.
        private static System.Text.Json.JsonElement Unwrap(System.Text.Json.JsonElement el)
        {
            // Dizi → ilk JSON-veren bloğu aç.
            if (el.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                {
                    var inner = Unwrap(item);
                    if (inner.ValueKind is System.Text.Json.JsonValueKind.Object or System.Text.Json.JsonValueKind.Array
                        && !inner.TryGetProperty("$type", out _))
                        return inner;
                }
                return el;
            }

            if (el.ValueKind != System.Text.Json.JsonValueKind.Object) return el;

            // CallToolResult.structuredContent — tool dönüşü doğrudan burada.
            if (el.TryGetProperty("structuredContent", out var sc)
                && sc.ValueKind is System.Text.Json.JsonValueKind.Object or System.Text.Json.JsonValueKind.Array)
                return sc.Clone();

            // TextContent / content[].text — JSON string içeren metin alanı (büyük `Text` ve küçük `text`).
            if (TryParseTextProp(el, "Text", out var t1)) return t1;
            if (TryParseTextProp(el, "text", out var t2)) return t2;

            if (el.TryGetProperty("content", out var content)
                && content.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var block in content.EnumerateArray())
                {
                    if (TryParseTextProp(block, "text", out var tc)) return tc;
                    if (TryParseTextProp(block, "Text", out var tcu)) return tcu;
                }
            }
            return el;
        }

        private static bool TryParseTextProp(System.Text.Json.JsonElement el, string prop, out System.Text.Json.JsonElement parsed)
        {
            parsed = default;
            if (!el.TryGetProperty(prop, out var txt) || txt.ValueKind != System.Text.Json.JsonValueKind.String)
                return false;
            var raw = (txt.GetString() ?? string.Empty).TrimStart();
            if (!raw.StartsWith('{') && !raw.StartsWith('[')) return false;
            try { parsed = System.Text.Json.JsonDocument.Parse(raw).RootElement.Clone(); return true; }
            catch { return false; }
        }
    }
}
