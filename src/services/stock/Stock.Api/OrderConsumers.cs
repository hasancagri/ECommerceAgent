namespace Stock.Api;

// 092 checkout koreografisi: Order BC'nin StockCommitRequested event'ini tüketir. Siparişin TÜM kalemlerini
// TEK transaction'da atomik commit eder — biri yetersizse HİÇBİRİ düşmez (store yapılmadan çıkılır → Marten
// SaveChanges hiçbir mutasyonu persist etmez, ya hep ya hiç) ve StockCommitFailed yayar. Hepsi Ok ise
// StockCommitted + her ürün için StockChangedEvent. İş hatası (yetersiz stok) event ile bildirilir; teknik
// hata fırlar → Wolverine retry/DLQ. orderId idempotent (ProductStock._processedOps — çift teslimat no-op).
// Süreç güdümlü (kullanıcı tetiklemez) → Domains/ dışı kök; ad = kaynak BC + Consumers (Order).
public class OrderConsumers
{
    [Transactional]
    public async Task Handle(IntegrationEvents.StockCommitRequested evt, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        var productIds = evt.Items.Select(i => i.ProductId).ToList();
        var stocks = await session.Query<ProductStock>().Where(x => productIds.Contains(x.ProductId)).ToListAsync(ct);
        var byProduct = stocks.ToDictionary(s => s.ProductId);

        // Önce tüm kalemleri in-memory commit DENE; biri düşerse hiç persist etmeden çık (atomik).
        foreach (var item in evt.Items)
        {
            if (!byProduct.TryGetValue(item.ProductId, out var stock))
            {
                await bus.PublishAsync(new IntegrationEvents.StockCommitFailed(evt.OrderId, StockResourceConstants.RECORD_NOT_FOUND));
                return;
            }

            var result = stock.Commit(item.Quantity, evt.OrderId);
            if (!result.IsSuccess)
            {
                await bus.PublishAsync(new IntegrationEvents.StockCommitFailed(
                    evt.OrderId, result.Messages.FirstOrDefault()?.Code ?? StockResourceConstants.STOCK_INSUFFICIENT));
                return;
            }
        }

        // Hepsi başarılı → tek transaction'da persist (Store'lar [Transactional] tek SaveChanges'ine girer).
        foreach (var stock in stocks)
            session.Store(stock);

        await bus.PublishAsync(new IntegrationEvents.StockCommitted(evt.OrderId));
        foreach (var stock in stocks)
            await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(stock.ProductId, stock.Quantity));
    }
}
