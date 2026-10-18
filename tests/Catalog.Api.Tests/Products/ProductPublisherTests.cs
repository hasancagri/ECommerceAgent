namespace Catalog.Api.Tests.Products;

// 052 T005: Product.SetPublisher — tek yayınevi zorunlu (her kitap bir yayınevi; boş Id reddi).
public class ProductPublisherTests
{
    private static Product NewProduct() =>
        Product.Create("Wuthering Heights", "0007350813", ProductType.Simple, Money.Create(100m)!, "", "").Data!;

    [Fact]
    public void SetPublisher_ValidId_Assigns()
    {
        var product = NewProduct();
        var publisherId = Guid.NewGuid();

        product.SetPublisher(publisherId).IsSuccess.ShouldBeTrue();

        product.PublisherId.ShouldBe(publisherId);
    }

    [Fact]
    public void SetPublisher_EmptyId_ReturnsError()
    {
        var product = NewProduct();

        var result = product.SetPublisher(Guid.Empty);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.VALUE_EMPTY);
    }
}