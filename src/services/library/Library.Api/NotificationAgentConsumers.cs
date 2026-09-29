namespace Library.Api;

// notification-agent worker'inin mail sonucu tüketicisi. Wolverine *Consumers (çoğul) adını
// keşfetMEZ — Program.cs IncludeType ile dahil eder.
public class NotificationAgentConsumers
{
    /// <summary>
    /// Mail gönderiminden HEMEN SONRA koşar (NotificationAgent sonucu NotificationSent ile geri yayınlar).
    /// Her gönderim denemesi kalıcı iz olur — append-only NotificationRecord (FR-007);
    /// "sent" / "no-email" / hata özeti. Alarm silinse de iz kalır.
    /// </summary>
    [Transactional]
    public void Handle(
        IntegrationEvents.NotificationSent evt,
        IDocumentSession session)
    {
        session.Store(new Domains.PriceAlarms.Entities.NotificationRecord
        {
            UserId = evt.UserId,
            ProductId = evt.ProductId,
            Email = evt.Email,
            Success = evt.Success,
            Detail = evt.Detail,
            CreatedAtUtc = DateTime.UtcNow,
        });
    }
}
