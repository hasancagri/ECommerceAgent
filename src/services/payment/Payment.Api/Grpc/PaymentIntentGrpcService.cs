using System.Globalization;
using Grpc.Core;
using Shared.Grpc.Payment;

namespace Payment.Api.Grpc;

// 094 EC-içi S2S gРПC servisi — Order start_payment ödeme niyeti + PG hosted link ister. Tek çağıran
// Order; mantık doğrudan servis class'ında (Features hop yok — conventions S2S kuralı). PG link'i
// başarıyla alınırsa PaymentIntent (Pending, pgPaymentRef yazılı) saklanır; mağaza kapalı/PG hata →
// is_success=false + error_code (niyet SAKLANMAZ). Kart verisi burada YOK (İlke VIII).
public sealed class PaymentIntentGrpcService(IDocumentSession session, PgPaymentLinkClient pgClient)
    : PaymentIntentService.PaymentIntentServiceBase
{
    public override async Task<CreateIntentAndLinkReply> CreateIntentAndLink(
        CreateIntentAndLinkRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;

        if (!decimal.TryParse(request.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            return Fail(PaymentResourceConstants.PAYMENT_AMOUNT_INVALID);
        if (!Guid.TryParse(request.OrderId, out var orderId) || !Guid.TryParse(request.UserId, out var userId))
            return Fail(PaymentResourceConstants.PAYMENT_ORDER_ID_REQUIRED);

        // PG hosted link iste (dış-sağlayıcı REST). Başarısızsa niyet yaratma (orphan yok).
        var pg = await pgClient.CreateLinkAsync(request.OrderId, amount, request.OrderSummary, ct);
        if (!pg.IsSuccess)
            return Fail(pg.ErrorCode ?? PaymentResourceConstants.PAYMENT_GATEWAY_UNAVAILABLE);

        // TxRef = OrderId (mağaza-üretimli tekil, callback korelasyonu ayrıca pgPaymentRef ile).
        var created = PaymentIntent.Create(
            orderId, userId, request.BasketRef, amount,
            txRef: request.OrderId, pgPaymentRef: pg.PgPaymentRef, hostedUrl: pg.LinkUrl);
        if (!created.IsSuccess)
            return Fail(created.Messages.FirstOrDefault()?.Code ?? PaymentResourceConstants.PAYMENT_AMOUNT_INVALID);

        session.Store(created.Data!);
        await session.SaveChangesAsync(ct);

        return new CreateIntentAndLinkReply { IsSuccess = true, HostedUrl = pg.LinkUrl, ErrorCode = string.Empty };
    }

    private static CreateIntentAndLinkReply Fail(string code)
        => new() { IsSuccess = false, HostedUrl = string.Empty, ErrorCode = code };
}
