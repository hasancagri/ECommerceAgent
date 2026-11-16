using Microsoft.AspNetCore.Mvc;
using Personalization.Api.Domains.SearchHistory.Features.Queries;
using Personalization.Api.Domains.SearchHistory.Features.Commands;

namespace Personalization.Api.Domains.SearchHistory;

// 013: arama geçmişi HTTP yüzeyi (Personalization'ın İLK dış ucu). Gateway /api/personalization/**.
// Grup .RequireAuthorization() → anonim 401 (geçmiş kişisel); kimlik token'dan (CurrentUser → sub),
// her uç yalnız istek sahibinin verisine dokunur. Named scope YOK (kişisel-veri erişimi, İlke V notu).
public static class SearchHistoryEndpointExtension
{
    public static void MapSearchHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/personalization/v1/search-history")
            .WithTags("SearchHistory")
            .RequireAuthorization();

        // GET — tekilleştirilmiş, sayfalı geçmiş (en yeni üstte). Boş geçmiş = 200 + boş liste.
        group.MapGet("", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            IMessageBus bus,
            ICurrentUser currentUser,
            HttpContext http,
            CancellationToken ct) =>
        {
            currentUser.Load(http.User);
            if (currentUser.Id == Guid.Empty) return Results.Unauthorized();

            var result = await bus.InvokeAsync<FeatureObjectResultModel<GetSearchHistory.SearchHistoryResponse>>(
                new GetSearchHistory.GetSearchHistoryQuery(currentUser.Id, page ?? 1, pageSize ?? 20), ct);

            return Results.Ok(result.Data);
        });

        // DELETE item — bir sorgunun (normalize eşleşen) TÜM kayıtlarını KALICI sil (idempotent).
        group.MapDelete("/item", async (
            [FromQuery] string query,
            IMessageBus bus,
            ICurrentUser currentUser,
            HttpContext http,
            CancellationToken ct) =>
        {
            currentUser.Load(http.User);
            if (currentUser.Id == Guid.Empty) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(query)) return Results.BadRequest();

            var result = await bus.InvokeAsync<FeatureResultModel>(
                new DeleteSearchHistoryItem.DeleteSearchHistoryItemCommand(currentUser.Id, query), ct);

            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        // DELETE all — kullanıcının tüm geçmişini KALICI temizle.
        group.MapDelete("", async (
            IMessageBus bus,
            ICurrentUser currentUser,
            HttpContext http,
            CancellationToken ct) =>
        {
            currentUser.Load(http.User);
            if (currentUser.Id == Guid.Empty) return Results.Unauthorized();

            var result = await bus.InvokeAsync<FeatureResultModel>(
                new ClearSearchHistory.ClearSearchHistoryCommand(currentUser.Id), ct);

            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });
    }
}
