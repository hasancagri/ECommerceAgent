using BasketAggregate = Basket.Api.Domains.Baskets.Basket;

namespace Basket.Api.Grpc.Checkout;

// chat siparis tamamlama — Order.Api sepet kalemlerini sunucu tarafinda okur (kalem
// sunucu-otoritesi; LLM'e girmez). 074: Features/ yalnizca kullanici/agent-tetikledigi slice'lari
// tutar — bu sorgu yalniz gRPC'den tuketildigi icin (kullanici yolu degil) buraya, cagiran class'in
// icine tasindi; eski Domains/Features/Queries/GetBasket.cs silindi. Bilincli tekrar: Agents/Queries/
// GetBasket.cs ile ayni sorguyu farkli amacla (agent vs S2S) tekrar eder, paylasilmaz.
public class BasketItemsGrpcService(IQuerySession session) : BasketQuery.BasketQueryBase
{
    public override async Task<GetBasketItemsReply> GetBasketItems(
        GetBasketItemsRequest request, ServerCallContext context)
    {
        var reply = new GetBasketItemsReply();

        var userId = Guid.Parse(request.UserId);
        var basket = await session.Query<BasketAggregate>()
            .FirstOrDefaultAsync(x => x.UserId == userId, context.CancellationToken);
        if (basket is null)
            return reply; // sepet yok -> bos (Order tarafi 'rejected' verir)

        reply.TotalPrice = (double)basket.GetTotalPrice();
        foreach (var item in basket.Items)
            reply.Items.Add(new BasketLine
            {
                ProductId = item.Id.ToString(),
                Name = item.Name,
                UnitPrice = (double)item.Price,
                Quantity = item.Quantity
            });

        return reply;
    }
}
