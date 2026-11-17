using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using Query = Personalization.Api.Domains.SearchHistory.Features.Queries.GetSearchHistory;

namespace Personalization.Api.Domains.SearchHistory.Features.Agents.Queries;

// 014 US2: kullanıcının tekilleştirilmiş arama geçmişi (okuma). Login ZORUNLU (sub token'dan) —
// yalnız çağıranın kaydı. Silme BU TOOL'da YOK (yazma=tık): "geçmişimi sil" isteğinde agent 013
// geçmiş ekranına yönlendirir. İnce sarmalayıcı: tool → IMessageBus → 013 GetSearchHistory query.
public static class GetSearchHistoryTool
{
    [McpServerToolType]
    public static class Tool
    {
        [McpServerTool(Name = "get_search_history")]
        [Description("Giriş yapmış kullanıcının tekilleştirilmiş arama geçmişini döner (sorgu, mod, " +
                     "kaç kez arandığı, son arama zamanı; en yeni üstte). YALNIZ çağıranın. Silme YOK.")]
        public static async Task<Query.SearchHistoryResponse> GetSearchHistory(
            IMessageBus bus, ICurrentUser currentUser, IHttpContextAccessor http,
            [Description("Sayfa numarası (1'den başlar)")] int page = 1,
            [Description("Sayfa boyutu (en fazla 100)")] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            currentUser.Load(http.HttpContext!.User);
            var result = await bus.InvokeAsync<FeatureObjectResultModel<Query.SearchHistoryResponse>>(
                new Query.GetSearchHistoryQuery(currentUser.Id, page, pageSize), cancellationToken);
            return result.Data ?? new Query.SearchHistoryResponse { Page = page, PageSize = pageSize };
        }
    }
}
