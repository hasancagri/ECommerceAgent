namespace ScoutAgent.Api.Domains.Conversations.Features.Queries;

// 007 US4: geçmişten bir sohbetin tam transkripti. Sahiplik — başkasınınki NotFound (varlık sızmaz).
public static class GetConversation
{
    public record Query(Guid Id, Guid UserId);

    public class MessageView
    {
        public string Role { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class Response
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public IReadOnlyList<MessageView> Messages { get; set; } = [];
    }

    public class GetConversationHandler(IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Query query, CancellationToken ct)
        {
            var conversation = await session.LoadAsync<Conversation>(query.Id, ct);
            if (conversation is null || conversation.UserId != query.UserId)
                return FeatureObjectResultModel<Response>.NotFound();

            var messages = conversation.Messages.Select(m => new MessageView
            {
                Role = m.Role.ToString(),
                Text = m.Text,
                CreatedAt = m.CreatedAt,
            }).ToList();

            return FeatureObjectResultModel<Response>.Ok(new Response
            {
                Id = conversation.Id,
                Title = conversation.Title,
                Messages = messages,
            });
        }
    }
}
