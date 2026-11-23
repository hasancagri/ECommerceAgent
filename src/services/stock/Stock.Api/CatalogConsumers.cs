namespace Stock.Api;

// 010/021: Catalog'un ProductAdded event'ini tüketir (kaynak=Catalog → conventions kaynak-adı kuralı).
// Yeni ürün doğunca stok defteri açar. 021 ikili sinyal (HİZALI): message.InStock=false (Kitapyurdu
// "SATIŞ YOK*") → adet 0 (tükenmiş, katalogda görünür ama satılamaz); true → rastgele 20-50. Bayrak/
// adet asla çelişmez. Gerçek adet İTHAL edilmez (Excel stok %97 boş). StockChanged yayar → Catalog
// stok kopyası → IsSalable. Idempotent: ProductId için kayıt zaten varsa atlar (re-delivery mükerrer
// açmaz). Rastgele ALTYAPIDA; StockItem.Create deterministik (İlke VI). Wolverine *Consumers (çoğul)
// adını keşfetMEZ → MessagingExtensions IncludeType ZORUNLU.
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

        var quantity = message.InStock ? Random.Shared.Next(20, 51) : 0; // satışta 20-50; tükenmiş 0
        var created = StockItem.Create(message.ProductId, quantity);
        if (!created.IsSuccess)
            throw new InvalidOperationException(
                $"StockItem.Create reddedildi (ProductId={message.ProductId}, Qty={quantity}).");

        session.Store(created.Data!);
        await bus.PublishAsync(new IntegrationEvents.StockChanged(message.ProductId, quantity));
    }
}
