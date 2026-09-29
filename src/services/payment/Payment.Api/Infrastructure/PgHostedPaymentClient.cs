namespace Payment.Api.Infrastructure;

// Payment.Api → dış PaymentGateway (DropShop) hosted-payment istemcisi. MerchantKey per-request
// X-Api-Key (statik header YOK). PG iyzico hosted checkout-form init'i sarar (CF V2 HMAC PG içinde);
// dönen hosted URL + PG referansı PaymentIntent'e yazılır. Kontrat: contracts/pg-external.md.
public sealed class PgHostedPaymentClient(HttpClient http, PaymentOptions options)
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

    public sealed record HostedPaymentResult(bool Success, string? HostedUrl, string? PgPaymentRef);

    private sealed record HostedPaymentRequest(decimal Amount, string Currency, string OrderRef, string CallbackUrl);
    private sealed record HostedPaymentReply(string HostedUrl, string PgPaymentRef);

    // hosted-payment başlat: {Amount,Currency,OrderRef=TxRef,CallbackUrl} → {HostedUrl,PgPaymentRef}.
    // Fail-closed: 4xx/5xx/erişilemez → Success=false (çağıran link üretmez, order Cancel eder).
    public async Task<HostedPaymentResult> StartHostedPaymentAsync(
        decimal amount, string txRef, string callbackUrl, string merchantKey, CancellationToken ct)
    {
        try
        {
            var baseUrl = options.PgBaseUrl.TrimEnd('/');
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/hosted-payment")
            {
                Content = JsonContent.Create(new HostedPaymentRequest(amount, "TRY", txRef, callbackUrl))
            };
            request.Headers.Add("X-Api-Key", merchantKey);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(CallTimeout);

            using var response = await http.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
                return new HostedPaymentResult(false, null, null);

            var reply = await response.Content.ReadFromJsonAsync<HostedPaymentReply>(cancellationToken: cts.Token);
            if (reply is null || string.IsNullOrWhiteSpace(reply.HostedUrl))
                return new HostedPaymentResult(false, null, null);

            return new HostedPaymentResult(true, reply.HostedUrl, reply.PgPaymentRef);
        }
        catch (HttpRequestException)
        {
            return new HostedPaymentResult(false, null, null);
        }
        catch (TaskCanceledException)
        {
            return new HostedPaymentResult(false, null, null);
        }
    }
}
