namespace ScoutAgent.Api.Domains.Conversations.Features.Queries;

// 007 US4/FR-010: kullanıcının sohbet geçmişi listesi — YALNIZ çağıranın (UserId index).
public static class ListConversations
{
    public record Query(Guid UserId);

    public class Item
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }

    public class Response
    {
        public IReadOnlyList<Item> Conversations { get; set; } = [];
    }

    public class ListConversationsHandler(IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Query query, CancellationToken ct)
        {
            if (query.UserId == Guid.Empty)
                return FeatureObjectResultModel<Response>.Ok(new Response());

            var rows = await session.Query<Conversation>()
                .Where(c => c.UserId == query.UserId)
                .ToListAsync(ct);

            var items = rows
                .OrderByDescending(c => c.UpdatedTime ?? c.CreatedTime)
                .Select(c => new Item
                {
                    Id = c.Id,
                    Title = c.Title,
                    UpdatedAt = c.UpdatedTime ?? c.CreatedTime,
                })
                .ToList();

            return FeatureObjectResultModel<Response>.Ok(new Response { Conversations = items });
        }
    }
}
