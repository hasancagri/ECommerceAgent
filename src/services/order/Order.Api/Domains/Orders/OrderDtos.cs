namespace Order.Api.Domains.Orders;

// hosted-CF start_payment yolunun (StartPayment → BasketItemsClientProxy) paylaştığı sipariş kalemi
// taşıma tipi. Command değil, paylaşılan DTO.
public static class OrderDtos
{
    // Quantity varsayilan 1 (geriye-uyumlu; agent yolu adet gonderir).
    public record OrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity = 1);
}
