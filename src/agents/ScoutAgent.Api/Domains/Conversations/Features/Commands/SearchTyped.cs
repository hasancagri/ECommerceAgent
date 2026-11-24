using System.Text.Json;
using ScoutAgent.Api.Agents;
using ScoutAgent.Api.Search;

namespace ScoutAgent.Api.Domains.Conversations.Features.Commands;

// 022 Tur-2: TİPLİ niyet → kartlar. DETERMİNİSTİK (LLM YOK): FilterInterpreter slotları search_books
// argümanlarına düzler → Catalog yorumlar (taksonomi/sort/binding/ad→id) + ParadeDB → kartlar. Intro
// şablon. Message = kullanıcının ham sorgusu (Conversation'a insan-metni olarak yazılır; 017 sidebar/
// geçmiş korunur). Cevap şekli ChatResponse (React aynı parse'ı kullanır). Başlık üretimi YOK
// (ilk mesaja düşer — Tur-2 LLM'siz kalsın). "devamını getir" = React Filters.Page+1 yollar.
public static class SearchTyped
{
    public record Command(Guid? ConversationId, string Message, BookSearchFilters Filters, Guid? UserId);

    public class SearchTypedHandler(IDocumentSession session, McpToolSource mcp)
    {
        private static readonly System.Text.Json.JsonSerializerOptions ItemsJsonOptions =
            new() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

        public async Task<FeatureObjectResultModel<ChatResponse>> Handle(Command command, CancellationToken ct)
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
                            return FeatureObjectResultModel<ChatResponse>.Error(added.Messages);
                    }
                }
                if (conversation is null)
                {
                    var started = Conversation.Start(userId, command.Message);
                    if (!started.IsSuccess)
                        return FeatureObjectResultModel<ChatResponse>.Error(started.Messages);
                    conversation = started.Data!;
                }
            }

            var client = await mcp.GetClientAsync("catalog", ct);
            string text;

            if (!string.IsNullOrWhiteSpace(command.Filters.SimilarToTitle))
            {
                // 024 BENZER akışı (iki-adım): önce başlıkla çapa kitabı bul (productId), sonra similar_books.
                // Catalog'un similar_books tool'u hazır (konu+ton örtüşmesi + hard filtre + WorkKey tekil).
                var title = command.Filters.SimilarToTitle!.Trim();
                var findArgs = new Dictionary<string, object?> { ["query"] = title, ["page"] = 1, ["pageSize"] = 1 };
                var findResult = await client.CallToolAsync("search_books", findArgs, cancellationToken: ct);
                var anchor = McpToolSource.DeserializeResult<SearchBooksResult>(findResult)?.Items.FirstOrDefault();

                if (anchor is null)
                    text = $"'{title}' adlı kitabı bulamadım, benzerlerini getiremedim.";
                else
                {
                    var count = command.Filters.PageSize is < 1 or > 20 ? 10 : command.Filters.PageSize;
                    var simArgs = new Dictionary<string, object?> { ["productId"] = anchor.Id, ["count"] = count };
                    var simResult = await client.CallToolAsync("similar_books", simArgs, cancellationToken: ct);
                    var sim = McpToolSource.DeserializeResult<SimilarBooksResult>(simResult);
                    collector.Add("similar_books", sim, null);
                    text = (sim?.Total ?? 0) == 0
                        ? $"'{anchor.Title}' kitabına benzer kitap bulamadım."
                        : $"'{anchor.Title}' kitabına benzer {sim!.Total} kitap buldum; aşağıda kartlarda.";
                }
            }
            else if (SemanticIntent.IsSemantic(command.Filters))
            {
                // 026 SEMANTİK akışı: betimleyici sorgu → semantic_search (anlam kNN). Rakip demir yoksa
                // (SemanticIntent guard'ı) buraya; filtre geçişi US3'te. BM25 KARIŞMAZ (rol-ayrımı).
                // LLM konu kelimelerini Text.Query'ye sızdırmışsa katla (anlam kaybı yok).
                var q = string.Join(" ", new[] { command.Filters.SemanticQuery, command.Filters.Text?.Query }
                    .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
                var count = command.Filters.PageSize is < 1 or > 20 ? 10 : command.Filters.PageSize;
                var semArgs = new Dictionary<string, object?> { ["query"] = q, ["count"] = count };
                var semResult = await client.CallToolAsync("semantic_search", semArgs, cancellationToken: ct);
                var sem = McpToolSource.DeserializeResult<SemanticSearchResult>(semResult);
                collector.Add("semantic_search", sem, null);
                text = (sem?.Total ?? 0) == 0
                    ? "Betimlemene uygun kitap bulamadım."
                    : $"'{command.Message}' için {sem!.Total} kitap buldum; aşağıda kartlarda.";
            }
            else
            {
                // Deterministik arama: slot → args → search_books (Catalog yorumlar).
                var args = FilterInterpreter.ToSearchArgs(command.Filters);
                var result = await client.CallToolAsync("search_books", args, cancellationToken: ct);
                var typed = McpToolSource.DeserializeResult<SearchBooksResult>(result);
                collector.Add("search_books", typed, args);

                var total = typed?.Total ?? 0;
                if (total == 0 || typed is null)
                    text = "Aradığın kritere uygun kitap bulamadım.";
                else if (command.Filters.Page > 1)
                {
                    // Sayfalama turu: ham mesajı ("devamını getir") tırnaklama — aralık-metni daha temiz.
                    var size = command.Filters.PageSize < 1 ? 10 : command.Filters.PageSize;
                    var start = (typed.Page - 1) * size + 1;
                    var end = start + typed.Items.Count - 1;
                    text = typed.Items.Count > 0
                        ? $"Sonraki sayfa — {start}–{end}, toplam {total}."
                        : "Başka sonuç kalmadı.";
                }
                else
                    text = $"'{command.Message}' için {total} kitap buldum; aşağıda kartlarda.";
            }
            var reply = new ChatReply { Text = text, Items = BuildItems(collector) };

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

            return FeatureObjectResultModel<ChatResponse>.Ok(new ChatResponse
            {
                ConversationId = conversationId,
                Reply = reply,
            });
        }

        private static IReadOnlyList<ChatReplyItem> BuildItems(ToolResultCollector collector) =>
            collector.Results
                .Where(r => r.Result is not null)
                .Select(r => new ChatReplyItem(r.Tool, r.Result!, r.Filters))
                .ToList();
    }
}
