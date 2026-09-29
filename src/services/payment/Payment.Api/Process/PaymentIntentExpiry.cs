namespace Payment.Api.Process;

// US2: terk-timeout iç süreci (kullanıcı tetiklemez → Domains/ dışı Process/). CreatePaymentIntent
// anında ScheduleAsync(PaymentIntentExpiryCheck, IntentTimeoutSeconds) kurulur; tick'te intent hâlâ
// Pending ise Expire + PaymentFailed("ABANDONED"). Callback vs timer yarışı: Status guard → hangisi önce
// commit ederse kazanır, diğeri no-op. Checkout watchdog (ScheduleAsync) emsali; Marten-backed dayanıklı.
public record PaymentIntentExpiryCheck(string TxRef);

public class PaymentIntentExpiry
{
    [Transactional]
    public async Task Handle(PaymentIntentExpiryCheck msg, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        var intent = await session.Query<PaymentIntent>().FirstOrDefaultAsync(p => p.TxRef == msg.TxRef, ct);
        if (intent is null)
            return;

        var result = intent.Expire();
        if (!result.IsSuccess || intent.Status != PaymentIntentStatus.Expired)
            return; // callback kazandı (Succeeded/Failed) → no-op

        session.Store(intent);
        await bus.PublishAsync(new IntegrationEvents.PaymentFailed(
            intent.OrderId, intent.Id, intent.TxRef, intent.FailureReason ?? "ABANDONED"));
    }
}
