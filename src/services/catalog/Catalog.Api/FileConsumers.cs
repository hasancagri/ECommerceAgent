namespace Catalog.Api;

// 004: File 'CoverIngested' → ürünün kapak adresi (097 kapaksızlık kararı TERSİNE — FLOW 6b).
// ISBN (Gtin) ile ürün bulunur; SetImage idempotent (aynı url yeniden set edilebilir — at-least-once
// güvenli). "Ürün yok" çoğu zaman sıralama kazası: fırlat → uniform retry merdiveni → error-queue
// (DLQ, 088 deseni). Wolverine *Consumers adını keşfetMEZ → MessagingExtensions IncludeType ZORUNLU.
public class FileConsumers
{
    public async Task Handle(IntegrationEvents.CoverIngested evt, IDocumentSession session, CancellationToken ct)
    {
        var product = await session.Query<Product>()
            .FirstOrDefaultAsync(p => p.Gtin == evt.Isbn, ct)
            ?? throw new InvalidOperationException(
                $"CoverIngested işlenemedi: ürün katalogda yok (Isbn={evt.Isbn}).");

        product.SetImage(evt.Url);
        session.Store(product);
        await session.SaveChangesAsync(ct);
    }
}
