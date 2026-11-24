using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using Query = Personalization.Api.Domains.Taste.Features.Queries.ComputeTaste;

namespace Personalization.Api.Domains.Taste.Features.Agents.Queries;

// 028 Dilim-4: giriş yapmış kullanıcının zevk-vektörü + sinyal sayısı (PERSONAL MCP tool; user JWT,
// GetSearchHistoryTool deseni). YALNIZ ScoutAgent tüketir (feed orkestrasyonu) → Catalog recommend_books'a
// taşır. Vektör OPAK (İlke VIII): değeri insan-yüzeye/LLM'e/loga çıkmaz, yalnız S2S makinaya. İnce sarmalayıcı.
public static class GetMyTasteTool
{
    [McpServerToolType]
    public static class Tool
    {
        [McpServerTool(Name = "get_my_taste")]
        [Description("Giriş yapmış kullanıcının zevk-vektörünü (embedding uzayı noktası) + sinyal sayısını " +
                     "döner — kişisel öneri için (ScoutAgent feed). YALNIZ çağıranın. Vektör opak sayı dizisi " +
                     "(kullanıcıya gösterilmez). signalCount<3 ise kişisel profil henüz zayıf (küratörlü önerilir).")]
        public static async Task<Query.TasteResponse> GetMyTaste(
            IMessageBus bus, ICurrentUser currentUser, IHttpContextAccessor http,
            CancellationToken cancellationToken = default)
        {
            currentUser.Load(http.HttpContext!.User);
            var result = await bus.InvokeAsync<FeatureObjectResultModel<Query.TasteResponse>>(
                new Query.ComputeTasteQuery(currentUser.Id), cancellationToken);
            return result.Data ?? new Query.TasteResponse();
        }
    }
}
