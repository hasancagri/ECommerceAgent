namespace Payment.Api.Grpc;

// Payment.Api → Customer.Api merchant API key istemcisi (PG hosted-payment X-Api-Key kaynağı).
// Customer MerchantKeyService'i makine token'ıyla (customer.read; PaymentTokenHandler) çağırır.
// first-party mağaza = tek merchant → merchantId taşınmaz. Fail-closed: bulunamaz/erişilemez → null
// (link üretilmez). Key ASLA UI/LLM'e sızmaz. 074: performans için REST'ten gRPC'ye taşındı.
public sealed class MerchantKeyClient(MerchantKeyService.MerchantKeyServiceClient client)
{
    private static readonly TimeSpan CallDeadline = TimeSpan.FromSeconds(5);

    public async Task<string?> GetKeyAsync(CancellationToken ct)
    {
        try
        {
            var reply = await client.GetKeyAsync(new GetMerchantKeyRequest(),
                deadline: DateTime.UtcNow.Add(CallDeadline), cancellationToken: ct);

            return reply.Found && !string.IsNullOrWhiteSpace(reply.MerchantKey) ? reply.MerchantKey : null;
        }
        catch (RpcException)
        {
            return null; // fail-closed: Customer erişilemez
        }
    }
}
