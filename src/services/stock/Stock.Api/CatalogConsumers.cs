namespace Stock.Api;

// 050/051: Catalog ProductAdded tüketicisi. Yayınlanan üründe barkod↔ProductId eşlemesini kurar ve ilk
// OnHand'i (InitialStock) MUTLAK yazar. İlk yayıncı = kitap import (051); feed söküldü (050).
// ad = kaynak BC + Consumers (Saga/CheckoutConsumers.cs ile aynı desen); checkout sağa katılım
// (commit/revert-commit) orada, kaynağı Checkout orchestrator.
public class CatalogConsumers
{
    [Transactional]
    public async Task Handle(
        IntegrationEvents.ProductAdded evt,
        IDocumentSession session,
        IMessageBus bus,
        CancellationToken ct)
    {
        // Eşleme idempotent upsert (Id = barkod; aynı event tekrarı aynı satırı ezer).
        session.Store(BarcodeLink.Create(evt.Barcode, evt.ProductId));

        var stock = await session.Query<ProductStock>()
            .FirstOrDefaultAsync(s => s.ProductId == evt.ProductId, ct);
        if (stock is null)
        {
            stock = ProductStock.Create(evt.ProductId, evt.InitialStock);
        }
        else
        {
            var set = stock.SetQuantity(evt.InitialStock);
            if (!set.IsSuccess)
                return; // negatif adet — kontrat gereği gelmez; gelirse yok say (log'suz sessiz değil: guard)
        }

        session.Store(stock);
        await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(evt.ProductId, stock.Quantity));
    }
}