using Payment.Api.Domains.Payments.Features.Commands;

namespace Payment.Api.Domains.Payments;

// PG dış webhook ucu. intents/live + intents S2S (Order.Api) gRPC'ye taşındı (bkz.
// Grpc/PaymentIntentGrpcService.cs) — İlke I genişletmesi: dış webhook/PSP callback'i istisna, REST kalır.
public static class PaymentIntentEndpointExtension
{
    public sealed record CallbackRequest(string TxRef, string? PgPaymentRef, string Status, string? ReasonCode);

    public static void AddPaymentIntentEndpoints(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/internal/payments")
            .WithTags("PaymentIntentInternal")
            .WithApiVersionSet(apiVersionSet);

        // PG → ödeme sonucu callback (HMAC imzalı; scope YOK). Geçersiz/eksik imza → 401, handler çağrılmaz.
        group.MapPost("/callback", async (
            HttpRequest http, PaymentOptions options, IMessageBus bus, CancellationToken ct) =>
        {
            // CallbackSignatureValidator yalnız marker ITransientDependency taşır → Scrutor
            // AsImplementedInterfaces onu concrete tip olarak KAYDETMEZ; endpoint'e concrete inject
            // edilince Minimal API onu body sanıp deserialize eder → 500. PaymentOptions (singleton,
            // resolvable) inject edilir, validator inline new'lenir (saf HMAC helper'ı).
            var validator = new CallbackSignatureValidator(options);

            http.EnableBuffering();
            string rawBody;
            using (var reader = new StreamReader(http.Body, Encoding.UTF8, leaveOpen: true))
                rawBody = await reader.ReadToEndAsync(ct);
            http.Body.Position = 0;

            var signature = http.Headers["X-Signature"].ToString();
            if (!validator.IsValid(rawBody, signature))
                return Results.Unauthorized();

            CallbackRequest? body;
            try
            {
                body = JsonSerializer.Deserialize<CallbackRequest>(rawBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }
            if (body is null || string.IsNullOrWhiteSpace(body.TxRef))
                return Results.BadRequest();

            var result = await bus.InvokeAsync<FeatureResultModel>(
                new HandlePaymentCallback.HandlePaymentCallbackCommand(
                    body.TxRef, body.PgPaymentRef, body.Status, body.ReasonCode), ct);

            // TxRef bulunamadı → 404 (PG retry); aksi 200 (idempotent no-op dahil).
            return result.IsSuccess ? Results.Ok() : Results.NotFound(result);
        }).AllowAnonymous();
    }
}
