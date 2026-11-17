using ScoutAgent.Api.Domains.Conversations.Features.Commands;
using ScoutAgent.Api.Domains.Conversations.Features.Queries;

namespace ScoutAgent.Api;

// 007 chat-api.md: React → Gateway → ScoutAgent. v1 tek-parça yanıt (stream yok, FR-013).
// /messages opsiyonel bearer (anonim US1); /conversations* giriş şart + sahiplik (US4).
public static class ChatEndpointExtension
{
    // history: anon istemci-transkript (devamlılık/sayfalama). Login'de yok sayılır.
    public record SendMessageRequest(
        Guid? ConversationId, string Message, IReadOnlyList<SendMessage.HistoryTurn>? History = null);

    public static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chat/v1").WithTags("ScoutChat");

        // POST /messages — opsiyonel bearer. Token varsa sub → kalıcı sohbet; yoksa anonim oturumluk.
        group.MapPost("/messages", async (
            SendMessageRequest request, ICurrentUser currentUser, HttpContext http,
            IMessageBus bus, ScoutAgent.Api.Options.ScoutOptions options,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return Results.BadRequest(new { message = "Mesaj boş olamaz." });
            // 014: kötüye-kullanım guard'ı — aşırı uzun mesaj reddedilir (maliyet/bağlam şişmesi).
            if (request.Message.Length > options.MaxMessageLength)
                return Results.BadRequest(new { message = $"Mesaj çok uzun (en fazla {options.MaxMessageLength} karakter)." });

            currentUser.Load(http.User);
            Guid? userId = currentUser.Id == Guid.Empty ? null : currentUser.Id;

            try
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<SendMessage.Response>>(
                    new SendMessage.Command(request.ConversationId, request.Message, userId, request.History), ct);

                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            }
            catch (Exception ex)
            {
                // Yerel model / MCP ucu erişilemez → 503 anlaşılır hata (FR-008). Exception LOGLANIR (teşhis).
                loggerFactory.CreateLogger("ScoutChat").LogError(ex,
                    "SendMessage patladı — msg='{Msg}' userId={UserId}", request.Message, userId);
                return Results.Problem(statusCode: 503, title: "Kâşif şu an hizmet veremiyor, biraz sonra tekrar dene.");
            }
        });

        // GET /conversations — kullanıcının sohbet geçmişi (giriş şart, yalnız kendi).
        group.MapGet("/conversations", async (
            ICurrentUser currentUser, HttpContext http, IMessageBus bus, CancellationToken ct) =>
        {
            currentUser.Load(http.User);
            var result = await bus.InvokeAsync<FeatureObjectResultModel<ListConversations.Response>>(
                new ListConversations.Query(currentUser.Id), ct);
            return Results.Ok(result.Data);
        }).RequireAuthorization();

        // GET /conversations/{id} — tam transkript (sahiplik; başkasınınki 404).
        group.MapGet("/conversations/{id:guid}", async (
            Guid id, ICurrentUser currentUser, HttpContext http, IMessageBus bus, CancellationToken ct) =>
        {
            currentUser.Load(http.User);
            var result = await bus.InvokeAsync<FeatureObjectResultModel<GetConversation.Response>>(
                new GetConversation.Query(id, currentUser.Id), ct);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
        }).RequireAuthorization();

        // PATCH /conversations/{id} — başlığı elle değiştir (017). Sahiplik 404, geçersiz başlık 400.
        group.MapPatch("/conversations/{id:guid}", async (
            Guid id, RenameConversationRequest request, ICurrentUser currentUser, HttpContext http,
            IMessageBus bus, CancellationToken ct) =>
        {
            currentUser.Load(http.User);
            var result = await bus.InvokeAsync<FeatureResultModel>(
                new RenameConversation.Command(id, currentUser.Id, request.Title ?? string.Empty), ct);
            if (result.IsSuccess) return Results.Ok(result);
            return IsNotFound(result) ? Results.NotFound() : Results.BadRequest(result);
        }).RequireAuthorization();

        // DELETE /conversations/{id} — kalıcı sil (017, HARD). Sahiplik 404, başarı 204.
        group.MapDelete("/conversations/{id:guid}", async (
            Guid id, ICurrentUser currentUser, HttpContext http, IMessageBus bus, CancellationToken ct) =>
        {
            currentUser.Load(http.User);
            var result = await bus.InvokeAsync<FeatureResultModel>(
                new DeleteConversation.Command(id, currentUser.Id), ct);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization();
    }

    public record RenameConversationRequest(string? Title);

    // Sahiplik/yokluk hatası (NotFound) ile doğrulama hatasını (başlık boş/uzun) ayır → 404 vs 400.
    private static bool IsNotFound(FeatureResultModel result) =>
        result.Messages?.Any(m => m.Code == CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND) == true;
}
