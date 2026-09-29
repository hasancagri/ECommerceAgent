namespace Order.Api.Grpc;

// Order.Api → Discount.Api aktif indirim istemcisi (S2S; makine token discount.read, SagaTokenHandler).
// start_payment tutarı hesaplarken sepet ürünlerinin O AN aktif indirim yüzdesini canlı sorar (vitrin
// snapshot'ına GÜVENMEZ — SC-004). FAIL-CLOSED: erişilemez → boş (indirim yok, liste fiyatı) — asla
// aşırı-indirim. İndirim = ödeme anında aktif olan (grace yok). AddressClient emsali.
public sealed class DiscountClient(DiscountQuery.DiscountQueryClient client)
{
    private static readonly TimeSpan CallDeadline = TimeSpan.FromSeconds(5);

    // productId → aktif indirim yüzdesi (1-99). Aktif indirimi olmayan ürün sözlükte YOKTUR.
    public async Task<IReadOnlyDictionary<Guid, int>> GetActiveAsync(
        IEnumerable<Guid> productIds, CancellationToken ct)
    {
        var request = new GetProductDiscountsRequest();
        request.ProductIds.AddRange(productIds.Select(id => id.ToString()));
        if (request.ProductIds.Count == 0)
            return new Dictionary<Guid, int>();

        try
        {
            var reply = await client.GetProductDiscountsAsync(
                request, deadline: DateTime.UtcNow.Add(CallDeadline), cancellationToken: ct);

            var map = new Dictionary<Guid, int>();
            foreach (var d in reply.Discounts)
                if (Guid.TryParse(d.ProductId, out var pid))
                    map[pid] = d.DiscountPercent;
            return map;
        }
        catch (RpcException) { return new Dictionary<Guid, int>(); }
    }
}
