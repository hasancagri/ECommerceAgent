namespace Personalization.Api;

// 012: app-BFF'in UserSearched event'ini tüketir (kaynak = Bff → conventions kaynak-adı kuralı:
// yayıncı adı + Consumers). Login aramayı ham log olarak append eder (UserSearchRecord). İdempotent
// DEĞİL — her event ayrı satır (tekrar da sinyal). Create reddederse (bozuk event) iş düşmez, loglanır.
// Wolverine *Consumers (çoğul) adını keşfetMEZ → MessagingExtensions IncludeType ZORUNLU.
public class BffConsumers(ILogger<BffConsumers> logger)
{
    public async Task Handle(IntegrationEvents.UserSearched message, IDocumentSession session,
        CancellationToken ct)
    {
        var created = UserSearchRecord.Create(
            message.UserId, message.Query, message.Mode, message.ResultCount, message.SearchedAt);
        if (!created.IsSuccess)
        {
            // Bozuk event işi durdurmaz (userId boş/query boş gelmemeli — BFF login-only yayınlar).
            logger.LogWarning("012 UserSearched reddedildi (userId={UserId}) — atlandı.", message.UserId);
            return;
        }

        session.Store(created.Data!);
        await session.SaveChangesAsync(ct);
    }
}
