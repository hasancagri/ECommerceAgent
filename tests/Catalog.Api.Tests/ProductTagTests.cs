namespace Catalog.Api.Tests;

// 009 T004/T011 (test-first, İlke VI): Product.SetTags — ham etiketleri BookTag'e (lowercase)
// çevirir, Türkçe karakterli olanın ASCII ikizini de ekler, değere göre tekilleştirir, tam-değiştirir.
// Geçersiz parça atlanır, boş liste geçerli.
public class ProductTagTests
{
    private static Product NewProduct() =>
        Product.Create("Kitap", "9789750000001", Money.Create(100m)!, "açıklama").Data!;

    [Fact]
    public void SetTags_AsciiOnly_StoresLowercase()
    {
        var product = NewProduct();

        product.SetTags(new[] { "distopya", "Gelecek" });

        product.Tags.Select(t => t.Value).ShouldBe(new[] { "distopya", "gelecek" });
    }

    [Fact]
    public void SetTags_TurkishChars_AddsAsciiTwin()
    {
        var product = NewProduct();

        product.SetTags(new[] { "karanlık" });

        product.Tags.Select(t => t.Value).ShouldBe(new[] { "karanlık", "karanlik" });
    }

    [Fact]
    public void SetTags_DuplicateValues_Dedupes()
    {
        var product = NewProduct();

        product.SetTags(new[] { "distopya", "Distopya", "DISTOPYA" });

        product.Tags.Select(t => t.Value).ShouldBe(new[] { "distopya" });
    }

    [Fact]
    public void SetTags_InvalidParts_Skipped()
    {
        var product = NewProduct();

        product.SetTags(new[] { "", "   ", "distopya" });

        product.Tags.Select(t => t.Value).ShouldBe(new[] { "distopya" });
    }

    [Fact]
    public void SetTags_EmptyList_ReturnsOkNoTags()
    {
        var product = NewProduct();

        var result = product.SetTags(Array.Empty<string>());

        result.IsSuccess.ShouldBeTrue();
        product.Tags.ShouldBeEmpty();
    }

    [Fact]
    public void SetTags_Replaces_PreviousTags()
    {
        var product = NewProduct();
        product.SetTags(new[] { "eski" });

        product.SetTags(new[] { "yeni" });

        product.Tags.Select(t => t.Value).ShouldBe(new[] { "yeni" });
    }
}
