
namespace Catalog.Api.Tests;

// 095: sade künye modeli — kimlik Gtin (ISBN), tek leaf kategori + editoryal ilgili kategoriler.
// Davranış testleri test-first (İlke VI). Sku/Tag/Dimensions/Seo/çoklu-kategori testleri 095'te söküldü.
public class ProductTests
{
    private static Product NewProduct(string name = "Kitap", string gtin = "9789750000001") =>
        Product.Create(name, gtin, Money.Create(100m)!, "açıklama").Data!;

    // --- Create (fiyat>0 var oluş invariant'ı + kimlik guard'ları) ---

    [Fact]
    public void Create_SetsBookIdentityAndDefaults()
    {
        var result = Product.Create("Kitap", "9789750000001", Money.Create(100m)!, "açıklama");

        result.IsSuccess.ShouldBeTrue();
        var product = result.Data!;
        product.Name.ShouldBe("Kitap");
        product.Gtin.ShouldBe("9789750000001");
        product.Description.ShouldBe("açıklama");
        product.Price.Amount.ShouldBe(100m);
        product.CategoryId.ShouldBe(Guid.Empty); // kategori ataması SetCategory ile (seed zorunlu kılar)
        product.RelatedCategoryIds.ShouldBeEmpty();
        product.AuthorIds.ShouldBeEmpty();
        product.ImageUrl.ShouldBeNull();
    }

    [Fact]
    public void Create_ZeroPrice_ReturnsError()
    {
        var result = Product.Create("Kitap", "9789750000001", Money.Zero(), "a");

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.PRODUCT_PRICE_REQUIRED);
        result.Data.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyName_ReturnsError(string name)
    {
        var result = Product.Create(name, "9789750000001", Money.Create(100m)!, "a");

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.PRODUCT_NAME_REQUIRED);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyGtin_ReturnsError(string gtin)
    {
        var result = Product.Create("Kitap", gtin, Money.Create(100m)!, "a");

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.PRODUCT_GTIN_REQUIRED);
    }

    // --- Rename ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_EmptyName_ReturnsError(string name)
    {
        var product = NewProduct();

        var result = product.Rename(name);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.PRODUCT_NAME_REQUIRED);
        product.Name.ShouldBe("Kitap");
    }

    [Fact]
    public void Rename_ValidName_ChangesName()
    {
        var product = NewProduct();

        product.Rename("Kitap 2").IsSuccess.ShouldBeTrue();
        product.Name.ShouldBe("Kitap 2");
    }

    // --- SetDescription (tek açıklama alanı — Short/Full 095'te birleşti) ---

    [Fact]
    public void SetDescription_ReplacesDescription()
    {
        var product = NewProduct();

        product.SetDescription("yeni açıklama").IsSuccess.ShouldBeTrue();
        product.Description.ShouldBe("yeni açıklama");
    }

    // --- Money VO + SetPrice ---

    [Fact]
    public void MoneyCreate_NegativeAmount_ReturnsNull()
    {
        Money.Create(-1m).ShouldBeNull();
    }

    [Fact]
    public void MoneyCreate_ValidAmount_CarriesAmountAndCurrency()
    {
        var money = Money.Create(149.90m)!;

        money.Amount.ShouldBe(149.90m);
        money.Currency.ShouldBe("TRY");
    }

    [Fact]
    public void SetPrice_ReplacesPrice()
    {
        var product = NewProduct();

        product.SetPrice(Money.Create(250m)!).IsSuccess.ShouldBeTrue();
        product.Price.Amount.ShouldBe(250m);
    }

    [Fact]
    public void SetPrice_ZeroAmount_ReturnsError()
    {
        var product = NewProduct();

        var result = product.SetPrice(Money.Zero());

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.PRODUCT_PRICE_REQUIRED);
        product.Price.Amount.ShouldBe(100m); // hatada fiyat değişmez
    }

    // --- SetCategory (tek leaf; üst kategori ürüne yazılmaz — ağaçtan çözülür) ---

    [Fact]
    public void SetCategory_ValidLeaf_AssignsSingleCategory()
    {
        var product = NewProduct();
        var leafId = Guid.NewGuid();

        product.SetCategory(leafId).IsSuccess.ShouldBeTrue();
        product.CategoryId.ShouldBe(leafId);
    }

    [Fact]
    public void SetCategory_EmptyGuid_ReturnsError()
    {
        var product = NewProduct();

        var result = product.SetCategory(Guid.Empty);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.PRODUCT_CATEGORY_REQUIRED);
        product.CategoryId.ShouldBe(Guid.Empty);
    }

    // --- SetRelatedCategories (editoryal liste; dedup + leaf hariç + boş Guid elenir) ---

    [Fact]
    public void SetRelatedCategories_DuplicateIds_Deduplicates()
    {
        var product = NewProduct();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        product.SetRelatedCategories(new[] { a, b, a }).IsSuccess.ShouldBeTrue();

        product.RelatedCategoryIds.ShouldBe(new[] { a, b });
    }

    [Fact]
    public void SetRelatedCategories_ContainsPrimaryLeaf_ExcludesLeaf()
    {
        var product = NewProduct();
        var leafId = Guid.NewGuid();
        var related = Guid.NewGuid();
        product.SetCategory(leafId);

        product.SetRelatedCategories(new[] { leafId, related }).IsSuccess.ShouldBeTrue();

        product.RelatedCategoryIds.ShouldBe(new[] { related }); // ilgili liste ata/leaf zinciriyle karışmaz
    }

    [Fact]
    public void SetRelatedCategories_EmptyAndDefaultIds_ResultsEmptyList()
    {
        var product = NewProduct();
        product.SetRelatedCategories(new[] { Guid.NewGuid() });

        product.SetRelatedCategories(new[] { Guid.Empty }).IsSuccess.ShouldBeTrue();

        product.RelatedCategoryIds.ShouldBeEmpty(); // tam-değiştirme: boş geçerli, önceki liste silinir
    }

    // --- SetImage ---

    [Fact]
    public void SetImage_AssignsAndClearsUrl()
    {
        var product = NewProduct();

        product.SetImage("img.png").IsSuccess.ShouldBeTrue();
        product.ImageUrl.ShouldBe("img.png");

        product.SetImage(null).IsSuccess.ShouldBeTrue();
        product.ImageUrl.ShouldBeNull();
    }
}
