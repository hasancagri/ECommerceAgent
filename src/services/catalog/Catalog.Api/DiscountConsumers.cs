namespace Catalog.Api;

// 003: Discount 'ProductDiscountChanged' → üründeki ETKİN ORAN kopyası (Product.DiscountRate).
// Rate null = indirim bitti/iptal (kopya temizlenir). Mutlak değer → idempotent.
// DLQ deseni (088): anomali yutulmaz, fırlatılır — uniform retry→error-queue policy'si işler
// (retry 1s/5s/15s; tükenirse mesaj error-queue'da bekler, incele/replay).
// Wolverine *Consumers (çoğul) adını keşfetMEZ → MessagingExtensions IncludeType ZORUNLU.
public class DiscountConsumers
{
    public async Task Handle(IntegrationEvents.ProductDiscountChanged evt, IDocumentSession session,
        CancellationToken ct)
    {
        var product = await session.LoadAsync<Product>(evt.ProductId, ct)
            ?? throw new InvalidOperationException(
                $"ProductDiscountChanged işlenemedi: ürün katalogda yok (ProductId={evt.ProductId}).");

        var result = product.SetDiscountRate(evt.Rate);
        if (!result.IsSuccess)
            throw new InvalidOperationException(
                $"ProductDiscountChanged reddedildi: geçersiz oran (ProductId={evt.ProductId}, Rate={evt.Rate}).");

        session.Store(product);
        await session.SaveChangesAsync(ct);
    }
}