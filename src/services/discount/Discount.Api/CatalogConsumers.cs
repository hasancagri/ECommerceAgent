namespace Discount.Api;

// 010: Catalog'un ProductDiscountImported event'ini tüketir (kaynak=Catalog → conventions kaynak-adı
// kuralı). İthal indirim → açık-uçlu Campaign (start=now, end=yok); hemen aktif olduğundan
// ProductDiscountChanged yayar → Catalog ürün indirim kopyası dolar (genel CampaignWindowProcess
// ertelenmiş kalır — bu import-özel yayın). now DIŞARIDAN (consumer'da UtcNow; domain saat okumaz).
// Wolverine *Consumers (çoğul) adını keşfetMEZ → MessagingExtensions IncludeType ZORUNLU.
// IDEMPOTENT: re-delivery mükerrer kampanya AÇMAZ — ürünün açık-uçlu + iptal-edilmemiş kampanyası
// zaten varsa atlar (import TEK üretici → "açık-uçlu, aktif" = import imzası; çoklu kampanya domain-
// meşru olduğundan basit ürün-başına unique index YANLIŞ olurdu). ATOMISİTE: [Transactional] +
// metot-param IMessageBus → Store + ProductDiscountChanged publish AYNI commit (outbox); elle
// SaveChangesAsync YOK, ctor-injected bus YOK (kök publisher enlist etmezdi).
public class CatalogConsumers
{
    [Transactional]
    public async Task Handle(IntegrationEvents.ProductDiscountImported message, IDocumentSession session,
        IMessageBus bus, CancellationToken ct)
    {
        var alreadyImported = await session.Query<Campaign>()
            .AnyAsync(c => c.ProductId == message.ProductId && c.EndsAt == null && !c.IsCancelled, ct);
        if (alreadyImported)
            return; // idempotent: ürünün açık-uçlu ithal kampanyası zaten var

        var created = Campaign.Create(message.ProductId, message.Rate, DateTimeOffset.UtcNow, endsAt: null);
        if (!created.IsSuccess)
            return; // geçersiz oran (normalde Catalog filtreler) — kampanya yok, iş durmaz

        session.Store(created.Data!);

        // Açık-uçlu kampanya hemen aktif → etkin oran = bu rate; Catalog kopyasına yansıt.
        await bus.PublishAsync(new IntegrationEvents.ProductDiscountChanged(message.ProductId, message.Rate));
    }
}
