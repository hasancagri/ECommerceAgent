
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

    // --- IsSalable (001+002: TEK tanım — fiyat>0 ∧ stok>0 ∧ ≥1 yazar; stok = StockChanged-fed kopya) ---

    private static Product SalableProduct()
    {
        var product = NewProduct(); // fiyatlı
        product.SetAuthors(new[] { Guid.NewGuid() });
        product.SetStockCount(10);
        return product;
    }

    [Fact]
    public void IsSalable_PricedAuthoredAndStocked_ReturnsTrue()
    {
        SalableProduct().IsSalable.ShouldBeTrue();
    }

    [Fact]
    public void IsSalable_WithoutStock_ReturnsFalse()
    {
        var product = NewProduct(); // fiyatlı
        product.SetAuthors(new[] { Guid.NewGuid() }); // stok kopyası 0 (default)

        product.IsSalable.ShouldBeFalse();
    }

    [Fact]
    public void IsSalable_WithoutPrice_ReturnsFalse()
    {
        var product = Product.Create("Kitap", "9789750000001", null, "a").Data!;
        product.SetAuthors(new[] { Guid.NewGuid() });
        product.SetStockCount(10);

        product.IsSalable.ShouldBeFalse();
    }

    [Fact]
    public void IsSalable_WithoutAuthors_ReturnsFalse()
    {
        var product = NewProduct(); // fiyatlı, yazarsız
        product.SetStockCount(10);

        product.IsSalable.ShouldBeFalse();
    }

    [Fact]
    public void IsSalable_AfterSetPriceCompletesCriteria_ReturnsTrue()
    {
        var product = Product.Create("Kitap", "9789750000001", null, "a").Data!;
        product.SetAuthors(new[] { Guid.NewGuid() });
        product.SetStockCount(5);

        product.SetPrice(Money.Create(120m)!);

        product.IsSalable.ShouldBeTrue();
    }

    [Fact]
    public void IsSalable_AfterAuthorsCleared_ReturnsFalse()
    {
        var product = SalableProduct();

        product.SetAuthors(Array.Empty<Guid>());

        product.IsSalable.ShouldBeFalse();
    }

    // --- SetStockCount (002: StockChanged-fed adet kopyası — yazarı yalnız tüketici kablosu) ---

    [Fact]
    public void SetStockCount_Zero_ClosesSalability()
    {
        var product = SalableProduct();

        product.SetStockCount(0).IsSuccess.ShouldBeTrue();

        product.StockCount.ShouldBe(0);
        product.IsSalable.ShouldBeFalse();
    }

    [Fact]
    public void SetStockCount_Negative_ReturnsErrorAndKeepsCurrent()
    {
        var product = SalableProduct(); // 10

        var result = product.SetStockCount(-1);

        result.IsSuccess.ShouldBeFalse();
        product.StockCount.ShouldBe(10);
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

    // --- SetDiscountRate (003: ProductDiscountChanged-fed oran kopyası; IsSalable'a GİRMEZ) ---

    [Fact]
    public void SetDiscountRate_Valid_SetsRate()
    {
        var product = NewProduct();

        product.SetDiscountRate(0.25m).IsSuccess.ShouldBeTrue();
        product.DiscountRate.ShouldBe(0.25m);
    }

    [Fact]
    public void SetDiscountRate_Null_ClearsRate()
    {
        var product = NewProduct();
        product.SetDiscountRate(0.25m);

        product.SetDiscountRate(null).IsSuccess.ShouldBeTrue();

        product.DiscountRate.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(0.91)]
    public void SetDiscountRate_OutOfRange_ReturnsError(decimal rate)
    {
        var product = NewProduct();

        product.SetDiscountRate(rate).IsSuccess.ShouldBeFalse();
        product.DiscountRate.ShouldBeNull();
    }

    [Fact]
    public void SetDiscountRate_DoesNotAffectSalability()
    {
        var product = SalableProduct();

        product.SetDiscountRate(0.5m);
        product.IsSalable.ShouldBeTrue();

        product.SetDiscountRate(null);
        product.IsSalable.ShouldBeTrue(); // indirim kriter DEĞİL
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

    // 021: SetCategory/SetRelatedCategories testleri SÖKÜLDÜ (kategori ekseni kalktı).

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

    // --- SetEnrichment (021: kapalı-sözlük taksonomi; geçersiz değer düşer/"belirsiz"e iner, iş durmaz) ---

    [Fact]
    public void SetEnrichment_ValidCanonicalValues_AssignsAll()
    {
        var product = NewProduct();

        var result = product.SetEnrichment(
            "roman", new[] { "karanlık", "ağır/düşündürücü" }, "yetişkin-orta",
            new[] { "distopya/ütopya", "güç/iktidar" }, 0.95m);

        result.IsSuccess.ShouldBeTrue();
        product.Genre.ShouldBe("roman");
        product.Moods.ShouldBe(new[] { "karanlık", "ağır/düşündürücü" });
        product.AudienceLevel.ShouldBe("yetişkin-orta");
        product.Themes.ShouldBe(new[] { "distopya/ütopya", "güç/iktidar" });
        product.EnrichmentConfidence.ShouldBe(0.95m);
    }

    [Fact]
    public void SetEnrichment_AliasValues_Canonicalizes()
    {
        var product = NewProduct();

        product.SetEnrichment("roman", new[] { "mizahi" }, "yetişkin-orta", new[] { "tarih" }, 0.8m);

        product.Moods.ShouldBe(new[] { "mizahi/absürt" }); // takma-ad → kanonik
        product.Themes.ShouldBe(new[] { "tarih/bellek" });
    }

    [Fact]
    public void SetEnrichment_UnmappableValues_Dropped()
    {
        var product = NewProduct();

        product.SetEnrichment("roman", new[] { "karanlık", "uydurma-ton" }, "yetişkin-orta",
            new[] { "distopya/ütopya", "kent/İstanbul" }, 0.7m);

        product.Moods.ShouldBe(new[] { "karanlık" });           // uydurma düştü
        product.Themes.ShouldBe(new[] { "distopya/ütopya" });   // yer adı düştü
    }

    [Fact]
    public void SetEnrichment_InvalidGenre_FallsBackToUnknown()
    {
        var product = NewProduct();

        product.SetEnrichment("şarkı sözü", Array.Empty<string>(), "", Array.Empty<string>(), 0.3m);

        product.Genre.ShouldBe(BookTaxonomy.Unknown);
        product.AudienceLevel.ShouldBe(BookTaxonomy.Unknown);   // boş seviye → belirsiz
    }

    [Fact]
    public void SetEnrichment_MoodsOverThree_TrimsToFirstThree()
    {
        var product = NewProduct();

        product.SetEnrichment("roman",
            new[] { "karanlık", "ağır/düşündürücü", "gizemli", "nostaljik" }, // 4 geçerli
            "yetişkin-orta", Array.Empty<string>(), 0.9m);

        product.Moods.Count.ShouldBe(3);
        product.Moods.ShouldBe(new[] { "karanlık", "ağır/düşündürücü", "gizemli" });
    }

    [Fact]
    public void SetEnrichment_ThemesOverFour_TrimsToFirstFour()
    {
        var product = NewProduct();

        product.SetEnrichment("roman", Array.Empty<string>(), "yetişkin-orta",
            new[] { "aşk/romantizm", "aile", "kimlik", "siyaset", "özgürlük" }, 0.9m); // 5 geçerli

        product.Themes.Count.ShouldBe(4);
        product.Themes.ShouldBe(new[] { "aşk/romantizm", "aile", "kimlik", "siyaset" });
    }

    [Fact]
    public void SetEnrichment_DuplicateValues_Deduped()
    {
        var product = NewProduct();

        product.SetEnrichment("roman", new[] { "karanlık", "karanlık" }, "yetişkin-orta",
            new[] { "aile", "aile" }, 0.9m);

        product.Moods.ShouldBe(new[] { "karanlık" });
        product.Themes.ShouldBe(new[] { "aile" });
    }

    [Theory]
    [InlineData(-0.5, 0.0)]
    [InlineData(1.5, 1.0)]
    [InlineData(0.42, 0.42)]
    public void SetEnrichment_Confidence_ClampedToUnitInterval(decimal input, decimal expected)
    {
        var product = NewProduct();

        product.SetEnrichment("roman", Array.Empty<string>(), "belirsiz", Array.Empty<string>(), input);

        product.EnrichmentConfidence.ShouldBe(expected);
    }

    [Fact]
    public void InStock_DerivedFromStockCount_TrueWhenPositive()
    {
        var product = NewProduct();
        product.InStock.ShouldBeFalse();     // stok 0 (varsayılan)

        product.SetStockCount(5);
        product.InStock.ShouldBeTrue();

        product.SetStockCount(0);
        product.InStock.ShouldBeFalse();     // tükenmiş
    }

    // --- SetWorkKey ---

    [Fact]
    public void SetWorkKey_AssignsValue()
    {
        var product = NewProduct();

        product.SetWorkKey("dostoyevski|suc ve ceza").IsSuccess.ShouldBeTrue();
        product.WorkKey.ShouldBe("dostoyevski|suc ve ceza");
    }
}
