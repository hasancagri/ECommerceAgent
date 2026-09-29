namespace Order.Api.Grpc;

// Order.Api → Customer.Api varsayılan adres istemcisi (S2S; makine token customer.read, SagaTokenHandler).
// start_payment siparişi kullanıcının varsayılan adresine bağlar (FR-001b). Fail-closed: adres yok/erişilemez
// → null → start_payment dostça Result hatası ("varsayılan adres bulunamadı"). Adres LLM'e girmez.
// performans için REST'ten gRPC'ye taşındı (BasketItemsClientProxy emsali).
public sealed class AddressClient(AddressQuery.AddressQueryClient client)
{
    private static readonly TimeSpan CallDeadline = TimeSpan.FromSeconds(5);

    public sealed record DefaultAddress(string Province, string District, string Street, string ZipCode, string Line);

    public async Task<DefaultAddress?> GetDefaultAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var reply = await client.GetDefaultAddressAsync(new GetDefaultAddressRequest
            {
                UserId = userId.ToString()
            }, deadline: DateTime.UtcNow.Add(CallDeadline), cancellationToken: ct);

            return reply.Found
                ? new DefaultAddress(reply.Province, reply.District, reply.Street, reply.ZipCode, reply.Line)
                : null;
        }
        catch (RpcException) { return null; }
    }
}
