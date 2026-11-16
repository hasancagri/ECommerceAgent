namespace Stock.Api;

// 010: Catalog'un ProductAdded event'ini tüketir (kaynak=Catalog → conventions kaynak-adı kuralı).
// Yeni ürün doğunca stok defteri açar: adet = rastgele 30-50 (demo/eğitim verisi — Excel stok
// kullanılamaz: %97 boş + "10+" string). StockChanged yayar → Catalog stok kopyası → IsSalable.
// Idempotent: ProductId için kayıt zaten varsa atlar (re-delivery mükerrer açmaz). Rastgele
// ALTYAPIDA; StockItem.Create(productId, onHand) deterministik (İlke VI). Wolverine *Consumers
// (çoğul) adını keşfetMEZ → MessagingExtensions IncludeType ZORUNLU.
public class CatalogConsumers(IMessageBus bus)
{
    public async Task Handle(IntegrationEvents.ProductAdded message, IDocumentSession session,
        CancellationToken ct)
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
        await session.SaveChangesAsync(ct);

        await bus.PublishAsync(new IntegrationEvents.StockChanged(message.ProductId, quantity));
    }
}
