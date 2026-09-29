namespace Order.Api.Grpc;

// Order.Api → Payment.Api hosted-CF ödeme girişimi istemcisi (senkron S2S; makine token payment.write,
// SagaTokenHandler). İki uç: canlı-intent sorgusu (re-use — order oluşturmadan önce) + link isteği.
// Fail-closed: erişilemez/hata → null (start_payment dostça Result hatası döner). Kontrat: contracts/store-internal.md.
// performans için REST'ten gRPC'ye taşındı (AddressClient/BasketItemsClientProxy emsali).
public sealed class PaymentIntentClient(PaymentIntentService.PaymentIntentServiceClient client)
{
    private static readonly TimeSpan CallDeadline = TimeSpan.FromSeconds(5);
    // CreateIntent sunucu tarafında dış PG'ye kadar uzanır (PgHostedPaymentClient CallTimeout=30s) — deadline ona göre.
    private static readonly TimeSpan CreateDeadline = TimeSpan.FromSeconds(35);

    public sealed record LiveIntent(Guid PaymentIntentId, Guid OrderId, string HostedUrl);
    public sealed record CreatedIntent(Guid PaymentIntentId, string HostedUrl);

    // Canlı Pending intent var mı (aynı kullanıcı+sepet)? Yoksa null.
    public async Task<LiveIntent?> GetLiveAsync(Guid userId, string basketRef, CancellationToken ct)
    {
        try
        {
            var reply = await client.GetLiveIntentAsync(new GetLiveIntentRequest
            {
                UserId = userId.ToString(),
                BasketRef = basketRef
            }, deadline: DateTime.UtcNow.Add(CallDeadline), cancellationToken: ct);

            return reply.Found
                ? new LiveIntent(Guid.Parse(reply.PaymentIntentId), Guid.Parse(reply.OrderId), reply.HostedUrl)
                : null;
        }
        catch (RpcException) { return null; }
    }

    // Link iste: order + txRef verilir → hosted URL döner. Başarısız → null.
    public async Task<CreatedIntent?> CreateAsync(
        Guid orderId, Guid userId, string basketRef, decimal amount, string txRef, CancellationToken ct)
    {
        try
        {
            var reply = await client.CreateIntentAsync(new CreateIntentRequest
            {
                OrderId = orderId.ToString(),
                UserId = userId.ToString(),
                BasketRef = basketRef,
                Amount = (double)amount,
                TxRef = txRef
            }, deadline: DateTime.UtcNow.Add(CreateDeadline), cancellationToken: ct);

            return reply.Success && !string.IsNullOrWhiteSpace(reply.HostedUrl)
                ? new CreatedIntent(Guid.Parse(reply.PaymentIntentId), reply.HostedUrl)
                : null;
        }
        catch (RpcException) { return null; }
    }
}
