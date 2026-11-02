namespace Catalog.Api.Tests.Publishers;

// 052 T003: Publisher fabrika davranışı (Author kalıbı) — normalize, boş ad reddi, ad immutability.
public class PublisherTests
{
    [Fact]
    public void PublisherCreate_ValidName_SetsNameAndNormalizedName()
    {
        var result = Publisher.Create(" Can Yayınları ");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Name.ShouldBe("Can Yayınları");
        result.Data.NormalizedName.ShouldBe(NameNormalization.Normalize("Can Yayınları"));
        result.Data.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void PublisherCreate_EmptyName_ReturnsError(string name)
    {
        var result = Publisher.Create(name);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.VALUE_EMPTY);
    }

    [Fact]
    public void PublisherCreate_DifferentSpacing_ShareUniquenessKey()
    {
        var a = Publisher.Create("Yapı  Kredi Yayınları").Data!;
        var b = Publisher.Create(" yapı kredi yayınları ").Data!;

        a.NormalizedName.ShouldBe(b.NormalizedName);
        a.Id.ShouldNotBe(b.Id);
    }

    [Fact]
    public void Publisher_ExposesNoPublicMutators()
    {
        var nameSetter = typeof(Publisher).GetProperty("Name")!.SetMethod;
        var normalizedSetter = typeof(Publisher).GetProperty("NormalizedName")!.SetMethod;

        nameSetter!.IsPublic.ShouldBeFalse();
        normalizedSetter!.IsPublic.ShouldBeFalse();
    }
}