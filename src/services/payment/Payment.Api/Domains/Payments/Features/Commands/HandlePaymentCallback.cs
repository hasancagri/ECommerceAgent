namespace Payment.Api.Domains.Payments.Features.Commands;

// US1/US2: PG hosted-payment sonucu callback'i. İmza doğrulaması ucta yapılır (US3); bu handler
// gövde doğrulanmış sayar. [Transactional] + durable outbox: intent durum yazımı + PaymentSucceeded/
// PaymentFailed yayını AYNI commit'te (commit yoksa event de yok → PG retry; commit varsa restart'ta
// yayınlanır, kayıp yok — FR-009). İdempotent: TxRef yok → NotFound (PG retry); Status != Pending → no-op.
public static class HandlePaymentCallback
{
    public record HandlePaymentCallbackCommand(string TxRef, string? PgPaymentRef, string Status, string? ReasonCode);

    public class HandlePaymentCallbackCommandHandler(IDocumentSession session, IMessageBus bus)
    {
        [Transactional]
        public async Task<FeatureResultModel> Handle(HandlePaymentCallbackCommand cmd, CancellationToken ct)
        {
            var intent = await session.Query<PaymentIntent>().FirstOrDefaultAsync(p => p.TxRef == cmd.TxRef, ct);
            if (intent is null)
                return FeatureResultModel.NotFound();

            // İdempotent: terminal (veya çift callback) → no-op Ok.
            if (intent.Status != PaymentIntentStatus.Pending)
                return FeatureResultModel.Ok();

            var isSuccess = string.Equals(cmd.Status, "Success", StringComparison.OrdinalIgnoreCase);
            if (isSuccess)
            {
                var r = intent.MarkSucceeded(cmd.PgPaymentRef);
                if (!r.IsSuccess) return FeatureResultModel.Error(r.Messages);

                session.Store(intent);
                await bus.PublishAsync(new IntegrationEvents.PaymentSucceeded(
                    intent.OrderId, intent.UserId, intent.Id, intent.TxRef, intent.Amount));
                return FeatureResultModel.Ok();
            }

            var f = intent.MarkFailed(cmd.ReasonCode ?? "DECLINED");
            if (!f.IsSuccess) return FeatureResultModel.Error(f.Messages);

            session.Store(intent);
            await bus.PublishAsync(new IntegrationEvents.PaymentFailed(
                intent.OrderId, intent.Id, intent.TxRef, cmd.ReasonCode ?? "DECLINED"));
            return FeatureResultModel.Ok();
        }
    }
}
