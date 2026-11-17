namespace ScoutAgent.Api.Domains.Conversations.Features.Commands;

// 017: kullanıcı sohbet başlığını elle değiştirir. Sahiplik — başkasınınki NotFound (varlık sızmaz).
// Guard + UpdatedTime değişmezliği aggregate'te (Conversation.Rename).
public static class RenameConversation
{
    public record Command(Guid Id, Guid UserId, string Title);

    public class RenameConversationHandler(IDocumentSession session)
    {
        public async Task<FeatureResultModel> Handle(Command command, CancellationToken ct)
        {
            var conversation = await session.LoadAsync<Conversation>(command.Id, ct);
            if (conversation is null || conversation.UserId != command.UserId)
                return FeatureResultModel.NotFound();

            var renamed = conversation.Rename(command.Title);
            if (!renamed.IsSuccess)
                return FeatureResultModel.Error(renamed.Messages);

            session.Store(conversation);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}
