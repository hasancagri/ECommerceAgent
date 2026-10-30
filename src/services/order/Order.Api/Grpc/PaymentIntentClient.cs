using Grpc.Core;
using Shared.Grpc.Payment;

namespace Order.Api.Grpc;

// 094: Order → Payment.Api S2S istemcisi (makine token payment.write, SagaTokenHandler). start_payment
// tutarı hesapladıktan sonra ödeme niyeti + PG hosted link'i ister. FAIL-CLOSED: erişilemez →
// PAYMENT_GATEWAY_UNAVAILABLE (link üretilmez, Order commit edilmez). AddressClient emsali.
public sealed class PaymentIntentClient(PaymentIntentService.PaymentIntentServiceClient client)
{
    private static readonly TimeSpan CallDeadline = TimeSpan.FromSeconds(10);

    public async Task<IntentLinkResult> CreateAsync(
        Guid orderId, Guid userId, string basketRef, decimal amount, string orderSummary, CancellationToken ct)
    {
        try
        {
            var reply = await client.CreateIntentAndLinkAsync(new CreateIntentAndLinkRequest
            {
                OrderId = orderId.ToString(),
                UserId = userId.ToString(),
                BasketRef = basketRef,
                Amount = amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Currency = "TRY",
                StoreName = string.Empty,   // Payment config'ten
                OrderSummary = orderSummary,
                ReturnUrl = string.Empty    // Payment config'ten
            }, deadline: DateTime.UtcNow.Add(CallDeadline), cancellationToken: ct);

            return reply.IsSuccess
                ? new IntentLinkResult(true, reply.HostedUrl, null)
                : new IntentLinkResult(false, string.Empty, reply.ErrorCode);
        }
        catch (RpcException)
        {
            return new IntentLinkResult(false, string.Empty, OrderResourceConstants.PAYMENT_GATEWAY_UNAVAILABLE);
        }
    }
}

public sealed record IntentLinkResult(bool IsSuccess, string HostedUrl, string? ErrorCode);
