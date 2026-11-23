namespace Stock.Api;

// 010: Catalog'un ProductAdded event'ini tüketir (kaynak=Catalog → conventions kaynak-adı kuralı).
// Yeni ürün doğunca stok defteri açar: adet = rastgele 30-50 (demo/eğitim verisi — Excel stok
// kullanılamaz: %97 boş + "10+" string). StockChanged yayar → Catalog stok kopyası → IsSalable.
// Idempotent: ProductId için kayıt zaten varsa atlar (re-delivery mükerrer açmaz). Rastgele
// ALTYAPIDA; StockItem.Create(productId, onHand) deterministik (İlke VI). Wolverine *Consumers
// (çoğul) adını keşfetMEZ → MessagingExtensions IncludeType ZORUNLU.
// ATOMISİTE: [Transactional] + metot-param IMessageBus → StockItem yazımı ile StockChanged publish
// AYNI commit'te (Marten outbox). Elle SaveChangesAsync YOK; ctor-injected bus da YOK (kök publisher
// enlist etmezdi). Crash penceresi kapalı: ya ikisi de olur ya hiçbiri (kayıp StockChanged → kalıcı 0 stok).
public class CatalogConsumers
{
    [Transactional]
    public async Task Handle(IntegrationEvents.ProductAdded message, IDocumentSession session,
        IMessageBus bus, CancellationToken ct)
    {
        var exists = await session.Query<StockItem>()
            .AnyAsync(s => s.ProductId == message.ProductId, ct);
        if (exists)
            return; // ürün başına tek kayıt (idempotent)

        var quantity = Random.Shared.Next(30, 51); // 30-50 dahil
        var created = StockItem.Create(message.ProductId, quantity);
        if (!created.IsSuccess)
            throw new InvalidOperationException(
                $"StockItem.Create reddedildi (ProductId={message.ProductId}, Qty={quantity}).");

        session.Store(created.Data!);
        await bus.PublishAsync(new IntegrationEvents.StockChanged(message.ProductId, quantity));
    }
}
