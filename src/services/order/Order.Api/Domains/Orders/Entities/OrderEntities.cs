namespace Order.Api.Domains.Orders.Entities;

public class OrderItem
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public decimal UnitPrice { get; private set; }

    // siparis kalemi adedi (varsayilan 1, geriye-uyumlu).
    public int Quantity { get; private set; } = 1;

    private OrderItem()
    {
    }

    public static OrderItem Create(Guid productId, string productName, decimal unitPrice, int quantity = 1)
    {
        return new OrderItem
        {
            ProductId = productId,
            ProductName = productName,
            UnitPrice = unitPrice,
            Quantity = quantity
        };
    }
}