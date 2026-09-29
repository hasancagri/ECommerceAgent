namespace Customer.Api.Grpc.HostedPayment;

// Payment.Api PG hosted-payment X-Api-Key kaynağı — YAPISAL S2S kanal (makine token customer.read).
// MerchantKey MCP/agent'a çıkmaz — yalnız bu kanaldan döner. 074: tek çağıranı bu servis olduğu için
// sorgu doğrudan buraya gömülü (Address gRPC deseni — bilinçli tekrar, eski internal REST ucu söküldü).
public class MerchantKeyGrpcService(IQuerySession session) : MerchantKeyService.MerchantKeyServiceBase
{
    public override async Task<GetMerchantKeyReply> GetKey(
        GetMerchantKeyRequest request, ServerCallContext context)
    {
        // first-party mağaza = tek merchant → merchantId taşınmaz.
        var merchant = await session.Query<MerchantInformation>()
            .FirstOrDefaultAsync(context.CancellationToken);

        return merchant is null
            ? new GetMerchantKeyReply { Found = false }
            : new GetMerchantKeyReply { Found = true, MerchantKey = merchant.MerchantKey };
    }
}
