namespace Catalog.Api;

// 002: Stock 'StockChanged' → üründeki adet KOPYASI (Product.StockCount) + IsSalable stok bacağı.
// Mutlak değer taşır → idempotent (son değer kazanır, at-least-once güvenli).
// DLQ desteği (088 deseni): anomali YUTULMAZ, fırlatılır — MessagingExtensions'taki uniform policy
// retry merdiveninden (1s/5s/15s) geçirir, tükenirse mesaj error-queue'ya düşer (incele/replay).
// "Ürün henüz yok" çoğu zaman sıralama kazasıdır; retry kendini onarır, kalıcıysa DLQ'da bekler.
// Wolverine *Consumers (çoğul) adını keşfetMEZ → MessagingExtensions IncludeType ZORUNLU.
public class StockConsumers
{
    public async Task Handle(IntegrationEvents.StockChanged evt, IDocumentSession session, CancellationToken ct)
    {
        var product = await session.LoadAsync<Product>(evt.ProductId, ct)
            ?? throw new InvalidOperationException(
                $"StockChanged işlenemedi: ürün katalogda yok (ProductId={evt.ProductId}).");

        var result = product.SetStockCount(evt.OnHand);
        if (!result.IsSuccess)
            throw new InvalidOperationException(
                $"StockChanged reddedildi: geçersiz adet (ProductId={evt.ProductId}, OnHand={evt.OnHand}).");

        session.Store(product);
        await session.SaveChangesAsync(ct);
    }
}