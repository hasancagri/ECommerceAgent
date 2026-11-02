
namespace Catalog.Api.Tests;

// 095: Category kapalı ağacın sade düğümü — tek davranış Create (ebeveyn doğumda sabit, FR-007).
// Rename/SetParent/Reorder/SetPublished/SetSeo testleri metotlarla birlikte söküldü.
public class CategoryTests
{
    // --- NameNormalization (teklik anahtarı) ---

    [Theory]
    [InlineData("Edebiyat", "EDEBIYAT")]
    [InlineData("  edebiyat  ", "EDEBIYAT")]
    [InlineData("popüler   kültür", "POPÜLER KÜLTÜR")]
    public void Normalize_TrimsCollapsesAndUppercases(string input, string expected)
    {
        NameNormalization.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public void Normalize_SameNameDifferentSpacing_ProducesSameKey()
    {
        NameNormalization.Normalize(" Popüler  Kültür ")
            .ShouldBe(NameNormalization.Normalize("popüler kültür"));
    }

    // --- Create ---

    [Fact]
    public void Create_ValidName_SetsNameAndNormalizedName()
    {
        var result = Category.Create("  Popüler   Kültür ");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Name.ShouldBe("Popüler   Kültür");
        result.Data.NormalizedName.ShouldBe("POPÜLER KÜLTÜR");
        result.Data.ParentCategoryId.ShouldBeNull();
        result.Data.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyName_ReturnsError(string name)
    {
        var result = Category.Create(name);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.VALUE_EMPTY);
    }

    [Fact]
    public void Create_WithParent_CarriesImmutableParent()
    {
        var parentId = Guid.NewGuid();

        var result = Category.Create("Popüler Kültür", parentId);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.ParentCategoryId.ShouldBe(parentId);
        // ebeveyn doğumda sabittir — aggregate taşıma/rename davranışı sunmaz (kapalı ağaç, FR-007)
        typeof(Category).GetMethod("SetParent").ShouldBeNull();
        typeof(Category).GetMethod("Rename").ShouldBeNull();
    }

    [Fact]
    public void Create_DifferentSpellings_ShareUniquenessKey()
    {
        var a = Category.Create("Edebiyat").Data!;
        var b = Category.Create(" EDEBİYAT ".Replace('İ', 'I')).Data!;

        a.NormalizedName.ShouldBe(b.NormalizedName);
        a.Id.ShouldNotBe(b.Id);
    }

    // --- Ad immutability: public setter yok ---

    [Fact]
    public void Create_Properties_ExposeNoPublicSetters()
    {
        var nameSetter = typeof(Category).GetProperty("Name")!.SetMethod;
        var normalizedSetter = typeof(Category).GetProperty("NormalizedName")!.SetMethod;
        var parentSetter = typeof(Category).GetProperty("ParentCategoryId")!.SetMethod;

        nameSetter!.IsPublic.ShouldBeFalse();
        normalizedSetter!.IsPublic.ShouldBeFalse();
        parentSetter!.IsPublic.ShouldBeFalse();
    }
}
