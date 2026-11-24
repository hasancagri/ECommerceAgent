using System.Text.Json;
using ScoutAgent.Api.Agents;
using ScoutAgent.Api.Search;

namespace ScoutAgent.Api.Domains.Conversations.Features.Commands;

// 022 Tur-2: TİPLİ niyet → kartlar. DETERMİNİSTİK (LLM YOK): FilterInterpreter slotları search_books
// argümanlarına düzler → Catalog yorumlar (taksonomi/sort/binding/ad→id) + ParadeDB → kartlar. Intro
// şablon. Message = kullanıcının ham sorgusu (Conversation'a insan-metni olarak yazılır; 017 sidebar/
// geçmiş korunur). Cevap şekli SendMessage.Response (React aynı parse'ı kullanır). Başlık üretimi YOK
// (ilk mesaja düşer — Tur-2 LLM'siz kalsın). "devamını getir" = React Filters.Page+1 yollar.
public static class SearchTyped
{
    public record Command(Guid? ConversationId, string Message, BookSearchFilters Filters, Guid? UserId);

    public class SearchTypedHandler(IDocumentSession session, McpToolSource mcp)
    {
        private static readonly System.Text.Json.JsonSerializerOptions ItemsJsonOptions =
            new() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

        public async Task<FeatureObjectResultModel<SendMessage.Response>> Handle(Command command, CancellationToken ct)
        {
            var isAuthenticated = command.UserId is Guid uid && uid != Guid.Empty;
            var collector = new ToolResultCollector();

            // Girişli: kayıt yükle/oluştur (sahiplik) + kullanıcı mesajını (ham sorgu) ekle. (SendMessage deseni.)
            Conversation? conversation = null;
            if (isAuthenticated)
            {
                var userId = command.UserId!.Value;
                if (command.ConversationId is Guid existingId)
                {
                    var found = await session.LoadAsync<Conversation>(existingId, ct);
                    if (found is not null && found.UserId == userId)
                    {
                        conversation = found;
                        var added = conversation.AddUserMessage(command.Message);
                        if (!added.IsSuccess)
                            return FeatureObjectResultModel<SendMessage.Response>.Error(added.Messages);
                    }
                }
                if (conversation is null)
                {
                    var started = Conversation.Start(userId, command.Message);
                    if (!started.IsSuccess)
                        return FeatureObjectResultModel<SendMessage.Response>.Error(started.Messages);
                    conversation = started.Data!;
                }
            }

            // Deterministik arama: slot → args → search_books (Catalog yorumlar).
            var args = FilterInterpreter.ToSearchArgs(command.Filters);
            var client = await mcp.GetClientAsync("catalog", ct);
            var result = await client.CallToolAsync("search_books", args, cancellationToken: ct);
            var typed = McpToolSource.DeserializeResult<SearchBooksResult>(result);
            collector.Add("search_books", typed, args);

            var total = typed?.Total ?? 0;
            var text = total > 0
                ? $"'{command.Message}' için {total} kitap buldum; aşağıda kartlarda."
                : $"'{command.Message}' için sonuç bulamadım.";
            var reply = new SendMessage.Reply { Text = text, Items = BuildItems(collector) };

            Guid conversationId;
            if (isAuthenticated && conversation is not null)
            {
                var itemsJson = reply.Items.Count > 0
                    ? System.Text.Json.JsonSerializer.Serialize(reply.Items, ItemsJsonOptions)
                    : null;
                conversation.AddAssistantMessage(reply.Text, itemsJson);
                session.Store(conversation);
                await session.SaveChangesAsync(ct);
                conversationId = conversation.Id;
            }
            else
            {
                conversationId = command.ConversationId ?? Guid.NewGuid();
            }

            return FeatureObjectResultModel<SendMessage.Response>.Ok(new SendMessage.Response
            {
                ConversationId = conversationId,
                Reply = reply,
            });
        }

        private static IReadOnlyList<SendMessage.ReplyItem> BuildItems(ToolResultCollector collector) =>
            collector.Results
                .Where(r => r.Result is not null)
                .Select(r => new SendMessage.ReplyItem(r.Tool, r.Result!, r.Filters))
                .ToList();
    }
}
