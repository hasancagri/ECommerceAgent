using Marten;

namespace Personalization.Api.Domains.SearchHistory.Features.Commands;

// 013 US3: bir benzersiz sorgunun (normalize eşleşen) TÜM ham kayıtlarını kullanıcı için KALICI siler.
// userId ZORUNLU (endpoint token'dan) — yalnız sahibin kayıtları. Türkçe-fold SQL'de olmadığından
// kullanıcının kayıtları çekilir, normalize eşleşenler silinir. Eşleşme yoksa da başarı (idempotent).
public static class DeleteSearchHistoryItem
{
    public record DeleteSearchHistoryItemCommand(Guid UserId, string Query);

    public class Handler
    {
        public async Task<FeatureResultModel> Handle(
            DeleteSearchHistoryItemCommand command, IDocumentSession session, CancellationToken ct)
        {
            var target = SearchHistoryProjection.Normalize(command.Query);

            var records = await session.Query<UserSearchRecord>()
                .Where(x => x.UserId == command.UserId)
                .ToListAsync(ct);

            foreach (var record in records.Where(r => SearchHistoryProjection.Normalize(r.Query) == target))
                session.Delete(record);

            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}
