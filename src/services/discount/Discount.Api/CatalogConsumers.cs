namespace Discount.Api;

// 010: Catalog'un ProductDiscountImported event'ini tüketir (kaynak=Catalog → conventions kaynak-adı
// kuralı). İthal indirim → açık-uçlu Campaign (start=now, end=yok); hemen aktif olduğundan
// ProductDiscountChanged yayar → Catalog ürün indirim kopyası dolar (genel CampaignWindowProcess
// ertelenmiş kalır — bu import-özel yayın). now DIŞARIDAN (consumer'da UtcNow; domain saat okumaz).
// Wolverine *Consumers (çoğul) adını keşfetMEZ → MessagingExtensions IncludeType ZORUNLU.
public class CatalogConsumers(IMessageBus bus)
{
    public async Task Handle(IntegrationEvents.ProductDiscountImported message, IDocumentSession session,
        CancellationToken ct)
    {
        var created = Campaign.Create(message.ProductId, message.Rate, DateTimeOffset.UtcNow, endsAt: null);
        if (!created.IsSuccess)
            return; // geçersiz oran (normalde Catalog filtreler) — kampanya yok, iş durmaz

        session.Store(created.Data!);
        await session.SaveChangesAsync(ct);

        // Açık-uçlu kampanya hemen aktif → etkin oran = bu rate; Catalog kopyasına yansıt.
        await bus.PublishAsync(new IntegrationEvents.ProductDiscountChanged(message.ProductId, message.Rate));
    }
}
