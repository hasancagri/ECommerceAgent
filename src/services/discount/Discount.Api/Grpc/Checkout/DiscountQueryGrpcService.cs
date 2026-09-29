using Discount.Api.Domains.ProductDiscounts;

namespace Discount.Api.Grpc.Checkout;

// US3: checkout S2S — Order.Api sağası ödeme tutarını hesaplarken ürünlerin AKTİF indirim yüzdesini
// canlı doğrular. FİYAT DÖNMEZ (yalnız yüzde). Yalnız pencere-içi (IsActiveAt) ProductDiscount döner —
// aktif değilse listede yer almaz = indirim yok (grace yok). Tek çağıranı Order olduğu için sorgu doğrudan
// servis class'ında (İLKE I / Domains-Features kuralı — ince S2S sarmalayıcı). Yetki: MapGrpcService
// .RequireAuthorization(discount.read) (Program.cs).
public class DiscountQueryGrpcService(IQuerySession session) : DiscountQuery.DiscountQueryBase
{
    public override async Task<GetProductDiscountsReply> GetProductDiscounts(
        GetProductDiscountsRequest request, ServerCallContext context)
    {
        var ids = request.ProductIds
            .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();

        var reply = new GetProductDiscountsReply();
        if (ids.Count == 0)
            return reply;

        var now = DateTime.UtcNow;
        var discounts = await session.Query<ProductDiscount>()
            .Where(x => ids.Contains(x.ProductId))
            .ToListAsync(context.CancellationToken);

        foreach (var discount in discounts.Where(d => d.IsActiveAt(now)))
            reply.Discounts.Add(new DiscountItem
            {
                ProductId = discount.ProductId.ToString(),
                DiscountPercent = discount.Percentage
            });

        return reply;
    }
}
