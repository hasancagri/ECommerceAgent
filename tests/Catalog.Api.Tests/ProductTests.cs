
namespace Catalog.Api.Tests;

// 095: sade künye modeli — kimlik Gtin (ISBN), tek leaf kategori + editoryal ilgili kategoriler.
// 001: satışa-uygunluk modeli — fiyatsız/yazarsız doğum serbest, satılabilirlik türetilmiş durum.
// Davranış testleri test-first (İlke VI).
public class ProductTests
{
    private static Product NewProduct(string name = "Kitap", string gtin = "9789750000001") =>
        Product.Create(name, gtin, Money.Create(100m)!, "açıklama").Data!;

    // --- Create (fiyat opsiyonel — verilirse >0; kimlik guard'ları) ---

    [Fact]
    public void Create_WithoutPrice_SucceedsWithNullPrice()
    {
        var result = Product.Create("Kitap", "9789750000001", null, "a");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Price.ShouldBeNull();
    }

    [Fact]
    public void Create_SetsBookIdentityAndDefaults()
    {
        var result = Product.Create("Kitap", "9789750000001", Money.Create(100m)!, "açıklama");

        result.IsSuccess.ShouldBeTrue();
        var product = result.Data!;
        product.Name.ShouldBe("Kitap");
        product.Gtin.ShouldBe("9789750000001");
        product.Description.ShouldBe("açıklama");
        product.Price!.Amount.ShouldBe(100m);
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

    // --- SetDescription (tek açıklama alanı — Short/Full 095'te birleşti) ---

    [Fact]
    public void SetDescription_ReplacesDescription()
    {
        var product = NewProduct();

        product.SetDescription("yeni açıklama").IsSuccess.ShouldBeTrue();
        product.Description.ShouldBe("yeni açıklama");
    }

    // --- SetPrice (001: fiyat sonradan atanır ve serbest değişir; atanan değer >0) ---

    [Fact]
    public void SetPrice_ValidAmount_SetsPrice()
    {
        var product = Product.Create("Kitap", "9789750000001", null, "a").Data!;

        product.SetPrice(Money.Create(250m)!).IsSuccess.ShouldBeTrue();
        product.Price!.Amount.ShouldBe(250m);
    }

    [Fact]
    public void SetPrice_OnPricedProduct_ChangesPrice()
    {
        var product = NewProduct(); // 100 TL

        product.SetPrice(Money.Create(175.50m)!).IsSuccess.ShouldBeTrue();
        product.Price!.Amount.ShouldBe(175.50m);
    }

    [Fact]
    public void SetPrice_ZeroAmount_ReturnsErrorAndKeepsCurrent()
    {
        var product = NewProduct(); // 100 TL

        var result = product.SetPrice(Money.Zero());

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.PRODUCT_PRICE_REQUIRED);
        product.Price!.Amount.ShouldBe(100m);
    }

    // SetAuthors testleri Products/ProductAuthorsTests.cs'te (001: boş liste geçerli).

    // --- IsSalable (001: TEK tanım — fiyat>0 ∧ stok>0 ∧ ≥1 yazar; stok ayağı Stock dönene dek boş geçer) ---

    [Fact]
    public void IsSalable_PricedAndAuthored_ReturnsTrue()
    {
        var product = NewProduct(); // fiyatlı
        product.SetAuthors(new[] { Guid.NewGuid() });

        product.IsSalable.ShouldBeTrue();
    }

    [Fact]
    public void IsSalable_WithoutPrice_ReturnsFalse()
    {
        var product = Product.Create("Kitap", "9789750000001", null, "a").Data!;
        product.SetAuthors(new[] { Guid.NewGuid() });

        product.IsSalable.ShouldBeFalse();
    }

    [Fact]
    public void IsSalable_WithoutAuthors_ReturnsFalse()
    {
        var product = NewProduct(); // fiyatlı, yazarsız

        product.IsSalable.ShouldBeFalse();
    }

    [Fact]
    public void IsSalable_AfterSetPriceCompletesCriteria_ReturnsTrue()
    {
        var product = Product.Create("Kitap", "9789750000001", null, "a").Data!;
        product.SetAuthors(new[] { Guid.NewGuid() });

        product.SetPrice(Money.Create(120m)!);

        product.IsSalable.ShouldBeTrue();
    }

    [Fact]
    public void IsSalable_AfterAuthorsCleared_ReturnsFalse()
    {
        var product = NewProduct();
        product.SetAuthors(new[] { Guid.NewGuid() });

        product.SetAuthors(Array.Empty<Guid>());

        product.IsSalable.ShouldBeFalse();
    }

    // --- SetBinding + SetPopularity (001: künye genişlemesi; satışa-uygunluğa etkisiz) ---

    [Fact]
    public void SetBinding_KnownValue_SetsBinding()
    {
        var product = NewProduct();

        product.SetBinding(BindingType.Ciltli).IsSuccess.ShouldBeTrue();
        product.Binding.ShouldBe(BindingType.Ciltli);
    }

    [Fact]
    public void SetPopularity_ValidValues_SetsCounters()
    {
        var product = NewProduct();

        var result = product.SetPopularity(rating: 4, ratingCount: 61, purchaseCount: 1021,
            toReadCount: 49, readingCount: 3, readCount: 37, favoriteCount: 1069);

        result.IsSuccess.ShouldBeTrue();
        product.Rating.ShouldBe(4);
        product.RatingCount.ShouldBe(61);
        product.PurchaseCount.ShouldBe(1021);
        product.ToReadCount.ShouldBe(49);
        product.ReadingCount.ShouldBe(3);
        product.ReadCount.ShouldBe(37);
        product.FavoriteCount.ShouldBe(1069);
    }

    [Fact]
    public void SetPopularity_ZeroValues_Succeeds()
    {
        var product = NewProduct();

        product.SetPopularity(null, 0, 0, 0, 0, 0, 0).IsSuccess.ShouldBeTrue();
        product.Rating.ShouldBeNull(); // hiç puanlanmamış kitap geçerli
    }

    [Fact]
    public void SetPopularity_NegativeCounter_ReturnsError()
    {
        var product = NewProduct();

        var result = product.SetPopularity(5, -1, 0, 0, 0, 0, 0);

        result.IsSuccess.ShouldBeFalse();
        product.RatingCount.ShouldBe(0); // mevcut durum değişmedi
    }

    [Fact]
    public void SetPopularity_RatingOutOfRange_ReturnsError()
    {
        var product = NewProduct();

        product.SetPopularity(6, 0, 0, 0, 0, 0, 0).IsSuccess.ShouldBeFalse();
        product.SetPopularity(-1, 0, 0, 0, 0, 0, 0).IsSuccess.ShouldBeFalse();
        product.Rating.ShouldBeNull();
    }

    [Fact]
    public void SetPopularity_DoesNotAffectSalability()
    {
        var product = NewProduct(); // fiyatlı, yazarsız → satışa kapalı

        product.SetPopularity(5, 100, 1000, 10, 10, 10, 10);

        product.IsSalable.ShouldBeFalse(); // sayaçlar kriter DEĞİL
    }

    // --- Money VO ---

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
