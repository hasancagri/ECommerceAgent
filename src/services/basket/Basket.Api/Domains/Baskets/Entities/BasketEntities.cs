namespace Basket.Api.Domains.Baskets.Entities;

public class BasketItem
{
    private BasketItem() { }

    public BasketItem(Guid id, string name, string? imageUrl, decimal price)
    {
        Id = id;
        Name = name;
        ImageUrl = imageUrl;
        Price = price;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string? ImageUrl { get; private set; }
    public decimal Price { get; private set; }

    public int Quantity { get; private set; } = 1;

    public void SetQuantity(int quantity) => Quantity = quantity;
}
