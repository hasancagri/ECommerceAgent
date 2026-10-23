using Newtonsoft.Json;
using Payment.Api.Options;

namespace Payment.Api.Domains.Payments;

// 094 (İlke I birincil kanalı + İlke VIII): PG→EC ödeme sonucu webhook'u. Endpoint ham gövdeyi HMAC
// doğrular (fail-closed), geçerliyse ProcessPaymentCallback.Command'i InvokeAsync eder. Handler
// [Transactional] — PaymentIntent güncellemesi + PaymentSucceeded/Failed yayını AYNI outbox'ta atomik.
// pgPaymentRef ile eşle, amount anti-tamper guard, MarkSucceeded/MarkFailed idempotent (yalnız gerçek
// Pending→terminal geçişinde event yayınla → çift webhook tek koreografi). Kontrat: contracts/store-webhook.md.
public static class ProcessPaymentCallback
{
    public const string SignatureHeader = "X-Signature";

    public record Command(string PgPaymentRef, string Status, decimal Amount, string? ReasonCode);

    [Transactional]
    public class Handler
    {
        public async Task<FeatureResultModel> Handle(
            Command cmd, IDocumentSession session, IMessageBus bus, CancellationToken ct)
        {
            var intent = await session.Query<PaymentIntent>()
                .FirstOrDefaultAsync(x => x.PgPaymentRef == cmd.PgPaymentRef, ct);
            if (intent is null)
                return FeatureResultModel.Error(new MessageItem { Code = PaymentResourceConstants.PAYMENT_CALLBACK_REJECTED });

            // Anti-tamper: webhook tutarı niyet tutarıyla eşleşmeli.
            if (cmd.Amount != intent.Amount)
                return FeatureResultModel.Error(new MessageItem { Code = PaymentResourceConstants.PAYMENT_AMOUNT_MISMATCH });

            var wasPending = intent.Status == PaymentIntentStatus.Pending;

            if (string.Equals(cmd.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                var r = intent.MarkSucceeded(cmd.PgPaymentRef);
                if (!r.IsSuccess) return FeatureResultModel.Error(r.Messages);
                session.Store(intent);
                if (wasPending) // yalnız gerçek geçişte yayınla (idempotent: çift webhook tek koreografi)
                    await bus.PublishAsync(new IntegrationEvents.PaymentSucceeded(
                        intent.OrderId, intent.UserId, intent.Id, intent.TxRef, intent.Amount));
            }
            else
            {
                var reason = cmd.ReasonCode ?? "FAILED";
                var r = intent.MarkFailed(reason);
                if (!r.IsSuccess) return FeatureResultModel.Error(r.Messages);
                session.Store(intent);
                if (wasPending)
                    await bus.PublishAsync(new IntegrationEvents.PaymentFailed(
                        intent.OrderId, intent.Id, intent.TxRef, reason));
            }

            return FeatureResultModel.Ok();
        }
    }
}

public static class PaymentCallbackEndpointExtension
{
    // Anonim + HMAC webhook ucu (dış-taraf callback; JWT yok). İmza/eşleşme/tamper → 404 fail-closed
    // (bilgi sızdırmaz). İdempotent başarı/tekrar → 200 (PG durable retry durur).
    public static void AddPaymentCallbackEndpoint(this WebApplication app)
    {
        app.MapPost("/payments/callback", async (
                HttpRequest req, IMessageBus bus, PgPaymentLinkOptions options, CancellationToken ct) =>
            {
                using var reader = new StreamReader(req.Body);
                var rawBody = await reader.ReadToEndAsync(ct);
                var signature = req.Headers[ProcessPaymentCallback.SignatureHeader].ToString();

                if (!CallbackSignatureValidator.Verify(options.CallbackSecret, rawBody, signature))
                    return Results.NotFound(); // fail-closed

                var body = JsonConvert.DeserializeObject<CallbackBody>(rawBody);
                if (body is null || string.IsNullOrEmpty(body.PgPaymentRef))
                    return Results.NotFound();

                var result = await bus.InvokeAsync<FeatureResultModel>(new ProcessPaymentCallback.Command(
                    body.PgPaymentRef, body.Status, body.Amount, body.ReasonCode), ct);

                return result.IsSuccess ? Results.Ok() : Results.NotFound();
            })
            .WithTags("payments")
            .AllowAnonymous();
    }

    // PG webhook ham gövdesi (camelCase; Newtonsoft case-insensitive eşler).
    private sealed class CallbackBody
    {
        public string PgPaymentRef { get; set; } = string.Empty;
        public string OrderRef { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
        public string? BankCode { get; set; }
        public string? ReasonCode { get; set; }
    }
}
