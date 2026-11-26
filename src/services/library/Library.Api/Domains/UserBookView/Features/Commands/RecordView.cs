using Marten;

namespace Library.Api.Domains.UserBookView.Features.Commands;

// Son Gezdiğim Kitaplar (2026-11-26): login kullanıcı kitap detayını açınca gezme kaydı. Deterministik
// Id (userId+productId) → session.Store idempotent upsert: tekrar gezme aynı satırı günceller (ViewedAt
// + snapshot tazelenir). userId ZORUNLU (endpoint token'dan verir) — başka kullanıcının kaydı ASLA.
public static class RecordView
{
    public record RecordViewCommand(
        Guid UserId, Guid ProductId, string Title, string Author, string? CoverUrl, string Isbn);

    public class Handler
    {
        public async Task<FeatureResultModel> Handle(
            RecordViewCommand command, IDocumentSession session, CancellationToken ct)
        {
            session.Store(UserBookView.Create(
                command.UserId, command.ProductId, command.Title, command.Author,
                command.CoverUrl, command.Isbn, DateTimeOffset.UtcNow));

            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}
