using ScoutAgent.Api.Domains.Conversations.Features.Commands;
using ScoutAgent.Api.Domains.Conversations.Features.Queries;
using ScoutAgent.Api.Search;

namespace ScoutAgent.Api.Domains.Conversations;

// 007 chat-api.md: React → Gateway → ScoutAgent. v1 tek-parça yanıt (stream yok, FR-013).
// /messages opsiyonel bearer (anonim US1); /conversations* giriş şart + sahiplik (US4).
public static class ConversationEndpointExtension
{
    public static void MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chat/v1").WithTags("ScoutChat");

        // 023: /messages (router+sub-agent) EMEKLİ — sohbet yalnız kitap keşfi, iki-faz /extract+/search.
        // 022 Tur-1: POST /extract — ham sorgu → TİPLİ niyet (BookSearchFilters). LLM structured output
        // (gün-kotası sayılır). Opsiyonel bearer (anonim serbest). React bunu alıp /search'e relay eder.
        group.MapPost("/extract", async (
            ExtractRequest request, ICurrentUser currentUser, HttpContext http,
            IMessageBus bus, ScoutAgent.Api.Options.ScoutOptions options,
            DailyUsageGuard usageGuard, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Query))
                return Results.BadRequest(new { message = "Sorgu boş olamaz." });
            if (request.Query.Length > options.MaxMessageLength)
                return Results.BadRequest(new { message = $"Sorgu çok uzun (en fazla {options.MaxMessageLength})." });

            currentUser.Load(http.User);
            Guid? userId = currentUser.Id == Guid.Empty ? null : currentUser.Id;
            var partition = ScoutUsageKey.Partition(userId, http.Connection.RemoteIpAddress?.ToString());
            var dailyCap = userId is null ? options.AnonPerDay : options.UserPerDay;
            if (!await usageGuard.TryConsumeAsync(partition, dailyCap, ct))
                return Results.Json(new { message = ScoutResourceConstants.TOO_MANY_REQUESTS }, statusCode: 429);

            try
            {
                var filters = await bus.InvokeAsync<BookSearchFilters>(
                    new ExtractIntent.Query(request.Query, request.Previous), ct);
                return Results.Ok(filters);
            }
            catch (Exception ex)
            {
                await usageGuard.ReleaseAsync(partition, ct);
                loggerFactory.CreateLogger("ScoutExtract").LogError(ex,
                    "Extract patladı — q='{Q}' userId={UserId}", request.Query, userId);
                return Results.Problem(statusCode: 503, title: "Kâşif şu an hizmet veremiyor, biraz sonra tekrar dene.");
            }
        }).RequireRateLimiting("chat");

        // 022 Tur-2: POST /search — TİPLİ niyet → kartlar. DETERMİNİSTİK (LLM YOK → kota sayılmaz).
        // Opsiyonel bearer; login → Conversation'a yazar (017). Message = kullanıcının ham sorgusu.
        group.MapPost("/search", async (
            SearchRequest request, ICurrentUser currentUser, HttpContext http,
            IMessageBus bus, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return Results.BadRequest(new { message = "Mesaj boş olamaz." });
            if (request.Filters is null)
                return Results.BadRequest(new { message = "Filtre boş olamaz." });

            currentUser.Load(http.User);
            Guid? userId = currentUser.Id == Guid.Empty ? null : currentUser.Id;

            var result = await bus.InvokeAsync<FeatureObjectResultModel<ChatResponse>>(
                new SearchTyped.Command(request.ConversationId, request.Message, request.Filters, userId), ct);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
        }).RequireRateLimiting("chat");

        // 028: "sana özel" feed (sorgu YOK → kişisel). Login ŞART (anonim 401; feed kişisel). get_my_taste
        // → recommend_books orkestrasyonu (deterministik, LLM yok → kota sayılmaz). React "sana özel" bunu çağırır.
        group.MapGet("/feed", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<ChatResponse>>(
                new Feed.Query(), ct);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
        }).RequireAuthorization();

        // 020-voice: POST /transcribe — ses → metin. Opsiyonel bearer (anonim serbest, FR-012). Çevirme
        // sohbetten AYRI gün-kotası (önek "tx:"). Guard slice'ta (400); OpenAI hatası → 503 + kota iade.
        group.MapPost("/transcribe", async (
            IFormFile file, ICurrentUser currentUser, HttpContext http,
            IMessageBus bus, ScoutAgent.Api.Options.ScoutOptions options,
            DailyUsageGuard usageGuard, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            if (file is null || file.Length <= 0)
                return Results.BadRequest(new { message = "Ses dosyası boş." });

            currentUser.Load(http.User);
            Guid? userId = currentUser.Id == Guid.Empty ? null : currentUser.Id;

            // Sohbet kotasından BAĞIMSIZ sayaç ("tx:" öneki) — STT pahalı, sohbeti yemesin.
            var partition = "tx:" + ScoutUsageKey.Partition(userId, http.Connection.RemoteIpAddress?.ToString());
            var dailyCap = userId is null ? options.TranscribeAnonPerDay : options.TranscribeUserPerDay;
            if (!await usageGuard.TryConsumeAsync(partition, dailyCap, ct))
                return Results.Json(new { message = ScoutResourceConstants.TOO_MANY_REQUESTS }, statusCode: 429);

            try
            {
                await using var stream = file.OpenReadStream();
                var query = new Transcribe.Query(
                    stream, file.FileName, file.ContentType, file.Length, options.MaxAudioBytes);
                var result = await bus.InvokeAsync<FeatureObjectResultModel<Transcribe.Response>>(query, ct);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            }
            catch (Exception ex)
            {
                // OpenAI erişilemez/çevirme hatası → kota iade (cevapsız kaldı) + 503 nazik hata (FR-008).
                await usageGuard.ReleaseAsync(partition, ct);
                loggerFactory.CreateLogger("ScoutTranscribe").LogError(ex,
                    "Transcribe patladı — userId={UserId} bytes={Bytes}", userId, file.Length);
                return Results.Problem(statusCode: 503, title: "Şu an çeviremedim, yazarak dene.");
            }
        }).DisableAntiforgery().RequireRateLimiting("chat");

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

    // 022 iki-faz: Tur-1 ham sorgu; Tur-2 tipli niyet + ham sorgu (Conversation'a insan-metni).
    public record ExtractRequest(string Query, BookSearchFilters? Previous = null);
    public record SearchRequest(Guid? ConversationId, string Message, BookSearchFilters Filters);

    // Sahiplik/yokluk hatası (NotFound) ile doğrulama hatasını (başlık boş/uzun) ayır → 404 vs 400.
    private static bool IsNotFound(FeatureResultModel result) =>
        result.Messages?.Any(m => m.Code == CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND) == true;
}
