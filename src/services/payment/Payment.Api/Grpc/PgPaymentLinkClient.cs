using System.Net.Http.Json;
using Payment.Api.Options;

namespace Payment.Api.Grpc;

// 094: EC→PG pay-by-link REST istemcisi (dış-sağlayıcı sınırı). POST {BaseUrl}/payment-links
// (X-Api-Key). Kart verisi GEÇMEZ (İlke VIII) — yalnız tutar/orderRef/dönüş. PG mağaza Live değilse
// STORE_NOT_LIVE döner → PAYMENT_STORE_NOT_LIVE'a çevir (start_payment Ayşe'ye taşır). Ulaşılamaz →
// PAYMENT_GATEWAY_UNAVAILABLE (fail-closed; link üretilmez). Kontrat: contracts/pg-payment-link.md.
public sealed class PgPaymentLinkClient(HttpClient http, PgPaymentLinkOptions options)
{
    public async Task<PgLinkResult> CreateLinkAsync(
        string orderRef, decimal amount, string orderSummary, CancellationToken ct)
    {
        var request = new
        {
            merchantId = options.MerchantId,
            amount,
            currency = "TRY",
            orderRef,
            returnUrl = options.ReturnUrl,
            storeName = options.StoreName,
            orderSummary
        };

        HttpResponseMessage response;
        try
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl}/payment-links")
            {
                Content = JsonContent.Create(request)
            };
            msg.Headers.Add("X-Api-Key", options.StoreApiKey);
            response = await http.SendAsync(msg, ct);
        }
        catch (HttpRequestException)
        {
            return PgLinkResult.Fail(PaymentResourceConstants.PAYMENT_GATEWAY_UNAVAILABLE);
        }

        if (response.IsSuccessStatusCode)
        {
            var ok = await response.Content.ReadFromJsonAsync<LinkOk>(ct);
            return ok is not null && !string.IsNullOrEmpty(ok.LinkUrl)
                ? PgLinkResult.Ok(ok.LinkUrl, ok.PgPaymentRef)
                : PgLinkResult.Fail(PaymentResourceConstants.PAYMENT_GATEWAY_UNAVAILABLE);
        }

        // Non-2xx: STORE_NOT_LIVE'ı ayır (mağaza satışa kapalı), diğer her şey gateway-unavailable.
        var code = PaymentResourceConstants.PAYMENT_GATEWAY_UNAVAILABLE;
        try
        {
            var err = await response.Content.ReadFromJsonAsync<LinkErr>(ct);
            var pgCode = err?.Messages?.FirstOrDefault()?.Code ?? string.Empty;
            if (pgCode.Contains("STORE_NOT_LIVE", StringComparison.OrdinalIgnoreCase))
                code = PaymentResourceConstants.PAYMENT_STORE_NOT_LIVE;
        }
        catch { /* gövde parse edilemedi → gateway-unavailable kalır */ }

        return PgLinkResult.Fail(code);
    }

    private sealed record LinkOk(string LinkUrl, string PgPaymentRef);
    private sealed record LinkErr(List<Msg>? Messages);
    private sealed record Msg(string? Code);
}

public sealed record PgLinkResult(bool IsSuccess, string LinkUrl, string PgPaymentRef, string? ErrorCode)
{
    public static PgLinkResult Ok(string linkUrl, string pgPaymentRef) => new(true, linkUrl, pgPaymentRef, null);
    public static PgLinkResult Fail(string errorCode) => new(false, string.Empty, string.Empty, errorCode);
}
