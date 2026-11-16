namespace Personalization.Api;

// 012: Catalog arama ucunun UserSearched event'ini tüketir (kaynak = Catalog → conventions kaynak-adı
// kuralı: yayıncı adı + Consumers). Login aramayı ham log olarak append eder (UserSearchRecord). İdempotent
// DEĞİL — her event ayrı satır (tekrar da sinyal). Create reddederse (bozuk event) iş düşmez, loglanır.
// Wolverine *Consumers (çoğul) adını keşfetMEZ → MessagingExtensions IncludeType ZORUNLU.
public class CatalogSearchConsumers(ILogger<CatalogSearchConsumers> logger)
{
    public async Task Handle(IntegrationEvents.UserSearched message, IDocumentSession session,
        CancellationToken ct)
    {
        var created = UserSearchRecord.Create(
            message.UserId, message.Query, message.Mode, message.ResultCount, message.SearchedAt);
        if (!created.IsSuccess)
        {
            // Bozuk event işi durdurmaz (userId boş/query boş gelmemeli — Catalog login-only yayınlar).
            logger.LogWarning("012 UserSearched reddedildi (userId={UserId}) — atlandı.", message.UserId);
            return;
        }

        session.Store(created.Data!);
        await session.SaveChangesAsync(ct);
    }
}
