namespace Catalog.Api.Tests.Products;

// 052 T004: Product.SetAuthors — çok-yazar, dedup, boş liste reddi (invariant: yayınlanan ürün ≥1 yazar).
public class ProductAuthorsTests
{
    private static Product NewProduct() =>
        Product.Create("Wuthering Heights", "0007350813", Money.Create(100m)!, "").Data!;

    [Fact]
    public void SetAuthors_MultipleIds_AssignsAllInOrder()
    {
        var product = NewProduct();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        product.SetAuthors(new[] { a, b }).IsSuccess.ShouldBeTrue();

        product.AuthorIds.ShouldBe(new[] { a, b });
    }

    [Fact]
    public void SetAuthors_DuplicateIds_Deduplicates()
    {
        var product = NewProduct();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        product.SetAuthors(new[] { a, b, a }).IsSuccess.ShouldBeTrue();

        product.AuthorIds.ShouldBe(new[] { a, b });
    }

    [Fact]
    public void SetAuthors_EmptyList_ReturnsError()
    {
        var product = NewProduct();

        var result = product.SetAuthors(Array.Empty<Guid>());

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.VALUE_EMPTY);
        product.AuthorIds.ShouldBeEmpty();
    }

    [Fact]
    public void SetAuthors_Replaces_PreviousAuthors()
    {
        var product = NewProduct();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        product.SetAuthors(new[] { first });
        product.SetAuthors(new[] { second });

        product.AuthorIds.ShouldBe(new[] { second });
    }
}
