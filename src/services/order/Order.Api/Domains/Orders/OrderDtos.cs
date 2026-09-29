namespace Order.Api.Domains.Orders;

// agent charge yolunun (PlaceOrderForAgent → PaymentAttempt → BasketItemsClientProxy) paylaştığı
// sipariş verisi DTO'ları. Eski adı CreateOrder idi (REST command söküldükten sonra boş kalmıştı);
// command değil, paylaşılan taşıma tipleri — dürüst ada taşındı.
public static class OrderDtos
{
    public record AddressDto(string Province, string District, string Street, string ZipCode, string Line);
    // Quantity varsayilan 1 (geriye-uyumlu; agent yolu adet gonderir).
    public record OrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity = 1);
}
