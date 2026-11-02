namespace Catalog.Api.Tests.Products;

// 052: Product.SetAuthors — çok-yazar, dedup. 001: boş liste GEÇERLİ (yazarsız kitap katalogda
// yaşar, satışa uygun sayılmaz — ≥1 yazar kuralı artık IsSalable kriterinde).
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
    public void SetAuthors_EmptyList_ClearsAuthors()
    {
        var product = NewProduct();
        product.SetAuthors(new[] { Guid.NewGuid() });

        product.SetAuthors(Array.Empty<Guid>()).IsSuccess.ShouldBeTrue();

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
