namespace ScoutAgent.Api.Domains.Conversations.Features.Commands;

// 017: kullanıcı sohbetini KALICI siler (HARD — 013 deseni; geri alınamaz). Sahiplik — başkasınınki
// NotFound (varlık sızmaz). Aggregate davranışı yok; silme handler'da session.Delete.
public static class DeleteConversation
{
    public record Command(Guid Id, Guid UserId);

    public class DeleteConversationHandler(IDocumentSession session)
    {
        public async Task<FeatureResultModel> Handle(Command command, CancellationToken ct)
        {
            var conversation = await session.LoadAsync<Conversation>(command.Id, ct);
            if (conversation is null || conversation.UserId != command.UserId)
                return FeatureResultModel.NotFound();

            session.Delete<Conversation>(command.Id);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}
