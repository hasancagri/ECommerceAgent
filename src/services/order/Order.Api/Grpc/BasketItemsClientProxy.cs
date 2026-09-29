namespace Order.Api.Grpc;

// hosted-CF start_payment — sepet kalemleri GetBasketItems gRPC ile SUNUCU tarafında okunur (kalem
// otoritesi: fiyat/adet LLM'e girmez). Fail-closed: Basket erişilemezse Unavailable (link üretilmez).
public sealed record BasketSnapshot(
    bool Reachable,
    IReadOnlyList<OrderDtos.OrderItemDto> Items,
    decimal TotalPrice)
{
    public bool IsEmpty => Items.Count == 0;

    public static readonly BasketSnapshot Unavailable = new(false, [], 0m);
}

public sealed class BasketItemsClientProxy(BasketQuery.BasketQueryClient client)
{
    private static readonly TimeSpan CallDeadline = TimeSpan.FromSeconds(5);

    public async Task<BasketSnapshot> GetItemsAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var reply = await client.GetBasketItemsAsync(new GetBasketItemsRequest
            {
                UserId = userId.ToString()
            }, deadline: DateTime.UtcNow.Add(CallDeadline), cancellationToken: ct);

            var items = reply.Items
                .Select(l => new OrderDtos.OrderItemDto(
                    Guid.Parse(l.ProductId), l.Name, (decimal)l.UnitPrice, l.Quantity))
                .ToList();

            return new BasketSnapshot(true, items, (decimal)reply.TotalPrice);
        }
        catch (RpcException)
        {
            return BasketSnapshot.Unavailable;
        }
    }

    // Deterministik sepet-içerik hash'i: kalemleri ProductId'ye göre sırala, ProductId:Quantity:UnitPrice
    // birleştir, SHA256 hex. re-use eşleşmesi (BasketRef) — sepet değişince hash değişir → yeni intent.
    public static string ComputeBasketRef(IReadOnlyList<OrderDtos.OrderItemDto> items)
    {
        var payload = string.Join("|", items
            .OrderBy(i => i.ProductId)
            .Select(i => $"{i.ProductId:N}:{i.Quantity}:{i.UnitPrice}"));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
