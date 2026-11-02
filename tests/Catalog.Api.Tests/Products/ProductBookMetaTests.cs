namespace Catalog.Api.Tests.Products;

// Kitapyurdu: Product.SetBookMeta (sayfa/yayın tarihi/dil) + SetTranslators (dedup/trim/boş).
public class ProductBookMetaTests
{
    private static Product NewProduct() =>
        Product.Create("17. Roman", "9789750858079", Money.Create(150m)!, "").Data!;

    [Fact]
    public void SetBookMeta_AllValues_SetsFields()
    {
        var product = NewProduct();
        var date = new DateOnly(2023, 7, 14);

        product.SetBookMeta(112, date, "Türkçe").IsSuccess.ShouldBeTrue();

        product.PageCount.ShouldBe(112);
        product.PublishedOn.ShouldBe(date);
        product.Language.ShouldBe("Türkçe");
    }

    [Fact]
    public void SetBookMeta_BlankLanguage_NormalizedToNull()
    {
        var product = NewProduct();

        product.SetBookMeta(null, null, "   ");

        product.Language.ShouldBeNull();
        product.PageCount.ShouldBeNull();
        product.PublishedOn.ShouldBeNull();
    }

    [Fact]
    public void SetTranslators_TrimsDedupesAndDropsBlank()
    {
        var product = NewProduct();

        product.SetTranslators(new[] { " Banu Gürsaler ", "Esra Özdoğan", "", "Banu Gürsaler" })
            .IsSuccess.ShouldBeTrue();

        product.Translators.ShouldBe(new[] { "Banu Gürsaler", "Esra Özdoğan" });
    }

    [Fact]
    public void SetTranslators_EmptyList_SucceedsWithNoTranslators()
    {
        var product = NewProduct();

        product.SetTranslators(Array.Empty<string>()).IsSuccess.ShouldBeTrue();

        product.Translators.ShouldBeEmpty();
    }

    [Fact]
    public void SetTranslators_Replaces_Previous()
    {
        var product = NewProduct();

        product.SetTranslators(new[] { "İlk Çevirmen" });
        product.SetTranslators(new[] { "İkinci Çevirmen" });

        product.Translators.ShouldBe(new[] { "İkinci Çevirmen" });
    }
}
