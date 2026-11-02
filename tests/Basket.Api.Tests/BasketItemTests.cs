namespace Basket.Api.Tests;

public class BasketItemTests
{
    [Fact]
    public void Constructor_SetsFields_WithDefaultQuantityOne()
    {
        var id = Guid.NewGuid();

        var item = new BasketItem(id, "product", "img.png", 100m);

        item.Id.ShouldBe(id);
        item.Name.ShouldBe("product");
        item.ImageUrl.ShouldBe("img.png");
        item.Price.ShouldBe(100m);
        item.Quantity.ShouldBe(1);
    }

    [Fact]
    public void SetQuantity_UpdatesQuantity()
    {
        var item = new BasketItem(Guid.NewGuid(), "product", null, 100m);

        item.SetQuantity(5);

        item.Quantity.ShouldBe(5);
    }
}