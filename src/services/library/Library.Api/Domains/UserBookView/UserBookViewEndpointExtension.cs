using Microsoft.AspNetCore.Mvc;
using Library.Api.Domains.UserBookView.Features.Commands;
using Library.Api.Domains.UserBookView.Features.Queries;

namespace Library.Api.Domains.UserBookView;

// Son Gezdiğim Kitaplar (2026-11-26): library'nin İLK REST yüzeyi. Gateway /api/library/**. Hepsi
// auth-zorunlu (geçmiş kişisel) → anonim 401; kimlik token'dan (CurrentUser → sub), kullanıcı yalnız
// KENDİ verisine dokunur. Named scope YOK (kişisel-veri; personalization 013 deseni).
public static class UserBookViewEndpointExtension
{
    public static void MapUserBookViewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/library/v1/viewed")
            .WithTags("RecentlyViewed")
            .RequireAuthorization();

        // POST — kitap detayı açılınca gezme kaydı (ateşle-unut; detay sayfası künye-snapshot taşır).
        group.MapPost("", async (
            [FromBody] RecordViewRequest body,
            IMessageBus bus,
            ICurrentUser currentUser,
            HttpContext http,
            CancellationToken ct) =>
        {
            currentUser.Load(http.User);
            if (currentUser.Id == Guid.Empty) return Results.Unauthorized();

            var result = await bus.InvokeAsync<FeatureResultModel>(
                new RecordView.RecordViewCommand(
                    currentUser.Id, body.ProductId, body.Title, body.Author, body.CoverUrl, body.Isbn), ct);

            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        // GET — en yeni 20 gezilen (en yeni önce). Boş geçmiş = 200 + boş liste.
        group.MapGet("", async (
            IMessageBus bus,
            ICurrentUser currentUser,
            HttpContext http,
            CancellationToken ct) =>
        {
            currentUser.Load(http.User);
            if (currentUser.Id == Guid.Empty) return Results.Unauthorized();

            var result = await bus.InvokeAsync<FeatureObjectResultModel<GetRecentlyViewed.RecentlyViewedResponse>>(
                new GetRecentlyViewed.GetRecentlyViewedQuery(currentUser.Id), ct);

            return Results.Ok(result.Data);
        });
    }

    // Gövde: detay sayfasının anlık snapshot'ı (yaklaşım A — BC-arası çağrı yok).
    public record RecordViewRequest(Guid ProductId, string Title, string Author, string? CoverUrl, string Isbn);
}
