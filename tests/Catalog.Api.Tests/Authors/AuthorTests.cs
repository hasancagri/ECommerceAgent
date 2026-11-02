namespace Catalog.Api.Tests.Authors;

// 052 T002: Author fabrika davranışı (Brand'in rename'i) — normalize, boş ad reddi, ad immutability.
public class AuthorTests
{
    [Fact]
    public void AuthorCreate_ValidName_SetsNameAndNormalizedName()
    {
        var result = Author.Create(" Emily Brontë ");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Name.ShouldBe("Emily Brontë");
        result.Data.NormalizedName.ShouldBe(NameNormalization.Normalize("Emily Brontë"));
        result.Data.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AuthorCreate_EmptyName_ReturnsError(string name)
    {
        var result = Author.Create(name);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.VALUE_EMPTY);
    }

    [Fact]
    public void AuthorCreate_DifferentSpacing_ShareUniquenessKey()
    {
        var a = Author.Create("Mary  Shelley").Data!;
        var b = Author.Create(" mary shelley ").Data!;

        a.NormalizedName.ShouldBe(b.NormalizedName);
        a.Id.ShouldNotBe(b.Id);
    }

    [Fact]
    public void Author_ExposesNoPublicMutators()
    {
        var nameSetter = typeof(Author).GetProperty("Name")!.SetMethod;
        var normalizedSetter = typeof(Author).GetProperty("NormalizedName")!.SetMethod;

        nameSetter!.IsPublic.ShouldBeFalse();
        normalizedSetter!.IsPublic.ShouldBeFalse();
    }
}