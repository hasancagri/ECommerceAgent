namespace Library.Api;

// Catalog'un fiyat değişim tüketicisi. Wolverine *Consumers (çoğul) adını keşfetMEZ —
// Program.cs IncludeType ile dahil eder.
public class CatalogConsumers
{
    /// <summary>
    /// Fiyat değişiminden HEMEN SONRA koşar (Catalog admin kaydeder kaydetmez, event'le).
    /// Alarm kurma burada DEĞİL (o CreatePriceAlarm komutu); burada mevcut alarm kayıtları bulunur
    /// ve alarm başına PriceAlarmTriggered yayınlanır — mail zincirinin başlangıcı.
    /// Tetik = OldPrice dolu VE yeni fiyat farklı (yön bakılmaz — R2). Alarm mutasyonu YOK:
    /// yaşayan abonelik (FR-004), kullanıcı kaldırana dek her değişimde tetiklenir. Alarm yoksa sessiz.
    /// </summary>
    public async Task Handle(
        IntegrationEvents.ProductChangedEvent evt,
        IDocumentSession session,
        IMessageBus bus,
        CancellationToken ct)
    {
        if (evt.OldPrice is null || evt.OldPrice == evt.Price)
            return;

        var alarms = await session.Query<PriceAlarm>()
            .Where(x => x.ProductId == evt.ProductId)
            .ToListAsync(ct);

        foreach (var alarm in alarms)
            await bus.PublishAsync(new IntegrationEvents.PriceAlarmTriggered(
                alarm.Id, alarm.UserId, alarm.Email,
                evt.ProductId, evt.Name,
                evt.OldPrice.Value, evt.Price));
    }
}
