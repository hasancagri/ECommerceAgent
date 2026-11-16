using Marten;

namespace Personalization.Api.Domains.SearchHistory.Features.Commands;

// 013 US3: kullanıcının TÜM arama geçmişini KALICI siler (clear-all). userId ZORUNLU — yalnız sahibin
// kayıtları. İstemci onayı UI'da. Silinen geri gelmez (yeniden aranırsa yeni kayıt — 012 yolu).
public static class ClearSearchHistory
{
    public record ClearSearchHistoryCommand(Guid UserId);

    public class Handler
    {
        public async Task<FeatureResultModel> Handle(
            ClearSearchHistoryCommand command, IDocumentSession session, CancellationToken ct)
        {
            session.DeleteWhere<UserSearchRecord>(x => x.UserId == command.UserId);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}
