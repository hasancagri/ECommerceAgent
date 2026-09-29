using static Shared.CheckoutMessages;

namespace Stock.Api.Saga;

// checkout orchestrator stok broker handler'ları. Komutları StockCommandsQueue'dan tüketir,
// ProductStock aggregate'ine doğrudan dokunur (Order.Api.Saga.CheckoutConsumers emsali — ara
// Domains/Features/Commands katmanı yok, TEK çağıranı bu sınıf olduğu için 074'te birleştirildi),
// sonucu reply kuyruğuna cascading message ile yayınlar. Domain idempotency (_processedOps, orderId)
// korunur. İş hatası → Permanent (telafi); altyapı hatası fırlar → Wolverine retry (temporal decoupling, US4).
// bu BC checkout sağasının KATILIMCISI (kullanıcı isteği değil, süreç güdümlü) → Domains/ dışı Saga/.
// Ad = kaynak BC + Consumers (kökteki CatalogConsumers ile aynı desen); kaynak burada Checkout orchestrator.
public class CheckoutConsumers
{
    [Transactional]
    public async Task<StockCommitted> Handle(CommitStockCommand cmd, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        var stock = await session.Query<ProductStock>().FirstOrDefaultAsync(x => x.ProductId == cmd.ProductId, ct);
        if (stock is null)
            return new StockCommitted(cmd.CheckoutId, cmd.ProductId, false, ErrorClass.Permanent, StockResourceConstants.RECORD_NOT_FOUND);

        var result = stock.Commit(cmd.Quantity, cmd.OrderId);
        if (!result.IsSuccess)
            return new StockCommitted(cmd.CheckoutId, cmd.ProductId, false, ErrorClass.Permanent, result.Messages.FirstOrDefault()?.Code);

        session.Store(stock);
        await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(stock.ProductId, stock.Quantity));

        return new StockCommitted(cmd.CheckoutId, cmd.ProductId, true, ErrorClass.None);
    }

    [Transactional]
    public async Task<StockCommitReverted> Handle(RevertCommitStockCommand cmd, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        var stock = await session.Query<ProductStock>().FirstOrDefaultAsync(x => x.ProductId == cmd.ProductId, ct);
        if (stock is null)
            return new StockCommitReverted(cmd.CheckoutId, cmd.ProductId, false, ErrorClass.Permanent, StockResourceConstants.RECORD_NOT_FOUND);

        var result = stock.RevertCommit(cmd.Quantity, cmd.OrderId);
        if (!result.IsSuccess)
            return new StockCommitReverted(cmd.CheckoutId, cmd.ProductId, false, ErrorClass.Permanent, result.Messages.FirstOrDefault()?.Code);

        session.Store(stock);
        await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(stock.ProductId, stock.Quantity));

        return new StockCommitReverted(cmd.CheckoutId, cmd.ProductId, true, ErrorClass.None);
    }
}