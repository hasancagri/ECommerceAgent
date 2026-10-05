namespace Customer.Api;

// 087 US1/FR-004: PG→store credential callback ucu (kontrat credential-callback.md). Dış-webhook (HTTP;
// İlke I sanksiyonlu istisna). Sıra: raw body oku → X-Signature HMAC doğrula (ÖNCE; geçersiz=400, gövde
// deserialize EDİLMEZ) → ReceiveMerchantCredentials invoke. İmza sonrası nötr (applied ya da nötr-red
// → 200; correlation doğruluğu + persist sonucu sızdırılmaz). MerchantKey asla log/trace/dönüşe yazılmaz.
public static class MerchantRegistrationCallbackEndpointExtension
{
    private const string SignatureHeader = "X-Signature";

    private sealed record CallbackPayload(Guid CorrelationId, Guid MerchantId, string MerchantKey, string? Status);

    public static void MapMerchantRegistrationCallbackEndpoint(this WebApplication app)
    {
        app.MapPost("/internal/merchant-registration/callback", async (
            HttpRequest request,
            Customer.Api.Onboarding.DropShopOnboardingOption options,
            IMessageBus bus,
            ILogger<CallbackSignatureValidator> logger,
            CancellationToken ct) =>
        {
            // CallbackSignatureValidator yalnız marker ITransientDependency taşır → Scrutor onu concrete
            // tip olarak KAYDETMEZ; endpoint'e concrete inject edilince Minimal API body sanıp deserialize
            // eder → 500 (Payment.Api /callback emsali). DropShopOnboardingOption (resolvable) inject edilir,
            // validator inline new'lenir (saf HMAC helper'ı).
            var validator = new CallbackSignatureValidator(options);

            // 1) Raw body (imza ham gövde üzerinden; deserialize ÖNCESİ).
            using var reader = new StreamReader(request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);

            // 2) HMAC doğrula — geçersiz/eksik → 400, hiçbir şey yapma (sahte callback kesilir).
            if (!validator.IsValid(rawBody, request.Headers[SignatureHeader]))
                return Results.StatusCode(StatusCodes.Status400BadRequest);

            // 3) İmza geçerli → gövde çöz. Çözülemez = PG kontrat ihlali → 400.
            CallbackPayload? payload;
            try
            {
                payload = System.Text.Json.JsonSerializer.Deserialize<CallbackPayload>(rawBody,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (System.Text.Json.JsonException)
            {
                return Results.StatusCode(StatusCodes.Status400BadRequest);
            }

            if (payload is null)
                return Results.StatusCode(StatusCodes.Status400BadRequest);

            // 4) Persist (correlation eşle + idempotent apply). İmza sonrası sonuç NÖTR → her durumda 200
            // (eşleşmeyen correlation / çift-callback sızdırılmaz). MerchantKey loglanmaz.
            var result = await bus.InvokeAsync<FeatureObjectResultModel<ReceiveMerchantCredentials.ReceiveMerchantCredentialsResponse>>(
                new ReceiveMerchantCredentials.ReceiveMerchantCredentialsCommand(
                    payload.CorrelationId, payload.MerchantId, payload.MerchantKey), ct);

            if (!result.IsSuccess)
                logger.LogWarning("Merchant registration callback nötr red (correlation {CorrelationId}).",
                    payload.CorrelationId);

            return Results.Ok();
        });
    }
}