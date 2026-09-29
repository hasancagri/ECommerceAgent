namespace Payment.Api.Grpc.HostedPayment;

// US1: hosted-CF S2S — canlı-intent re-use sorgusu + hosted ödeme linki üretimi. Tek çağıranı
// Order.Api olduğu için Features/Commands'teki CreatePaymentIntent + inline "live" sorgusu buraya
// gömüldü (Basket/Customer gRPC deseni — bilinçli tekrar). [Transactional] middleware'i (yalnız
// IMessageBus.InvokeAsync üzerinden Wolverine handler'larına uygulanır) burada YOK; auto-save yerini
// açık session.SaveChangesAsync() alır. bus.ScheduleAsync terk-timer için hâlâ kullanılır (aynı scope'lu
// IDocumentSession'a Wolverine.Marten outbox entegrasyonu üzerinden bağlıdır).
public class PaymentIntentGrpcService(
    IDocumentSession session,
    IMessageBus bus,
    MerchantKeyClient merchantKey,
    PgHostedPaymentClient gateway,
    PaymentOptions options) : PaymentIntentService.PaymentIntentServiceBase
{
    public override async Task<GetLiveIntentReply> GetLiveIntent(
        GetLiveIntentRequest request, ServerCallContext context)
    {
        var userId = Guid.Parse(request.UserId);
        var candidates = await session.Query<PaymentIntent>()
            .Where(p => p.UserId == userId && p.BasketRef == request.BasketRef
                        && p.Status == PaymentIntentStatus.Pending)
            .ToListAsync(context.CancellationToken);
        var live = candidates.FirstOrDefault(p => p.IsLive(options.IntentTimeoutSeconds, DateTime.UtcNow));

        return live is null
            ? new GetLiveIntentReply { Found = false }
            : new GetLiveIntentReply
            {
                Found = true,
                PaymentIntentId = live.Id.ToString(),
                OrderId = live.OrderId.ToString(),
                HostedUrl = live.HostedUrl
            };
    }

    public override async Task<CreateIntentReply> CreateIntent(
        CreateIntentRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var amount = (decimal)request.Amount;
        if (amount <= 0)
            return new CreateIntentReply { Success = false };

        var userId = Guid.Parse(request.UserId);

        // Re-use: aynı kullanıcı+sepet için canlı Pending intent varsa mevcut linki döndür.
        var candidates = await session.Query<PaymentIntent>()
            .Where(p => p.UserId == userId && p.BasketRef == request.BasketRef
                        && p.Status == PaymentIntentStatus.Pending)
            .ToListAsync(ct);
        var live = candidates.FirstOrDefault(p => p.IsLive(options.IntentTimeoutSeconds, DateTime.UtcNow));
        if (live is not null)
            return new CreateIntentReply
            {
                Success = true,
                PaymentIntentId = live.Id.ToString(),
                HostedUrl = live.HostedUrl,
                Reused = true
            };

        // MerchantKey (PG X-Api-Key) — tek kaynak Customer MerchantInformation. Yoksa fail-closed.
        var apiKey = await merchantKey.GetKeyAsync(ct);
        if (apiKey is null)
            return new CreateIntentReply { Success = false };

        var callbackUrl = $"https://{context.Host}/api/v1/internal/payments/callback";
        var pg = await gateway.StartHostedPaymentAsync(amount, request.TxRef, callbackUrl, apiKey, ct);
        if (!pg.Success)
            return new CreateIntentReply { Success = false };

        var orderId = Guid.Parse(request.OrderId);
        var create = PaymentIntent.Create(
            orderId, userId, request.BasketRef, amount, request.TxRef, pg.PgPaymentRef, pg.HostedUrl!);
        if (!create.IsSuccess)
            return new CreateIntentReply { Success = false };

        var intent = create.Data!;
        session.Store(intent);

        // Terk-timer: timeout sonunda hâlâ Pending ise Expire + PaymentFailed("ABANDONED").
        await bus.ScheduleAsync(new Payment.Api.Process.PaymentIntentExpiryCheck(intent.TxRef),
            TimeSpan.FromSeconds(options.IntentTimeoutSeconds));

        await session.SaveChangesAsync(ct);

        return new CreateIntentReply
        {
            Success = true,
            PaymentIntentId = intent.Id.ToString(),
            HostedUrl = intent.HostedUrl,
            Reused = false
        };
    }
}
