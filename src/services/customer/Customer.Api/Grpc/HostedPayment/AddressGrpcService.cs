namespace Customer.Api.Grpc.HostedPayment;

// (074'te REST'ten gRPC'ye taşındı): Order.Api (hosted-CF ödeme) siparişi varsayılan adrese bağlar
// — YAPISAL S2S kanal (makine token customer.read). Adres MCP/agent yüzeyinden AYRI. 074: tek çağıranı
// bu servis olduğu için sorgu doğrudan buraya gömülü (Basket/Stock gRPC deseni — bilinçli tekrar,
// Agents yüzeyiyle paylaşılmaz).
public class AddressGrpcService(IQuerySession session) : AddressQuery.AddressQueryBase
{
    public override async Task<GetDefaultAddressReply> GetDefaultAddress(
        GetDefaultAddressRequest request, ServerCallContext context)
    {
        var book = await session.Query<AddressBook>()
            .FirstOrDefaultAsync(b => b.UserId == Guid.Parse(request.UserId), context.CancellationToken);
        var target = book?.Addresses.FirstOrDefault(a => a.IsDefault) ?? book?.Addresses.FirstOrDefault();

        if (target is null)
            return new GetDefaultAddressReply { Found = false };

        var v = target.Value;
        return new GetDefaultAddressReply
        {
            Found = true,
            Province = v.Province,
            District = v.District,
            Street = v.Street,
            ZipCode = v.ZipCode,
            Line = v.Line
        };
    }
}
