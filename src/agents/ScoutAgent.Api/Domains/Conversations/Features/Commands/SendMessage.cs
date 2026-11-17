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
    public record Command(Guid? ConversationId, string Message, Guid? UserId);

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

            var tools = await toolSource.GetToolsAsync(isAuthenticated, ct);
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

            // Bağlam: girişli → [özet]+[son N] kayıttan (FR-012); anonim → tek mesaj.
            var contextMessages = BuildContext(conversation, command.Message, options.WindowSize);
            var runResponse = await agent.RunAsync(contextMessages, cancellationToken: ct);

            var reply = new Reply
            {
                Text = runResponse.Text ?? string.Empty,
                Items = ExtractItems(runResponse),
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

        private static List<MeaiChatMessage> BuildContext(Conversation? conversation, string latestMessage, int windowSize)
        {
            if (conversation is null)
                return [new MeaiChatMessage(MeaiChatRole.User, latestMessage)];

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

        // Tool çağrı sonuçlarını BİREBİR çıkarır (FunctionCall adı ↔ FunctionResult, CallId ile eşlenir).
        private static IReadOnlyList<ReplyItem> ExtractItems(AgentResponse response)
        {
            var toolNameByCallId = new Dictionary<string, string>();
            var items = new List<ReplyItem>();

            foreach (var message in response.Messages)
            {
                foreach (var content in message.Contents)
                {
                    switch (content)
                    {
                        case FunctionCallContent call:
                            toolNameByCallId[call.CallId] = call.Name;
                            break;
                        case FunctionResultContent result when result.Result is not null:
                            var tool = toolNameByCallId.TryGetValue(result.CallId, out var name) ? name : "unknown";
                            items.Add(new ReplyItem(tool,
                                System.Text.Json.JsonSerializer.SerializeToElement(result.Result)));
                            break;
                    }
                }
            }

            return items;
        }
    }
}
