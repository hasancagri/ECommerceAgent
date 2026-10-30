using Catalog.Api.Seeding;

namespace Catalog.Api.Tests;

// 091 T006 (İlke VI test-first): seed kaydının saf doğrulaması — satır atlatma kararı buradan çıkar
// (geçersiz satır ürün doğuramaz, seed kalanlarla sürer). Alanlar: contracts/seed-record.md.
public class SeedBookRecordTests
{
    private static SeedBookRecord NewRecord(
        string isbn = "9781442499713", decimal priceTry = 278.9m) => new()
    {
        Isbn = isbn,
        Title = "Güzel Yazılar",
        Authors = ["Jenny Han"],
        Publisher = "Simon & Schuster",
        PriceTry = priceTry,
        ImageUrl = "https://covers.example/x.jpg",
        CategoryPath = ["Edebiyat", "Roman (Çeviri)"],
    };

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyIsbn_ReturnsError(string isbn)
    {
        var result = NewRecord(isbn: isbn).Validate();

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.VALUE_EMPTY);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_NonPositivePrice_ReturnsError(decimal price)
    {
        var result = NewRecord(priceTry: price).Validate();

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.PRODUCT_PRICE_REQUIRED);
    }

    [Fact]
    public void Validate_ValidRecord_Succeeds()
    {
        NewRecord().Validate().IsSuccess.ShouldBeTrue();
    }

    // Kitapyurdu şema alanları (authorBios/translators/pageCount/publishedOn/language/categoryPath/
    // relatedCategoryPaths) System.Text.Json ile (case-insensitive) doğru deserialize olmalı. stock/binding
    // kolonları söküldü — JSON'da gelse bile bilinmeyen alan olarak sessizce yok sayılır (additive kural).
    [Fact]
    public void Deserialize_KitapyurduSchema_BindsNewFields()
    {
        const string json = """
        {
          "isbn": "9789750858079", "title": "17. Roman", "authors": ["Dag Solstad"],
          "authorBios": { "Dag Solstad": "Norveçli yazar." }, "translators": ["Banu Gürsaler"],
          "publisher": "Yapı Kredi Yayınları", "priceTry": 150.0, "pageCount": 112,
          "publishedOn": "2023-07-14", "language": "Türkçe", "binding": "Karton Kapak",
          "categoryPath": ["Edebiyat", "Roman (Çeviri)"],
          "relatedCategoryPaths": [["Edebiyat", "Polisiye"]],
          "imageUrl": null, "stock": 27, "description": "..."
        }
        """;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var record = JsonSerializer.Deserialize<SeedBookRecord>(json, options)!;

        record.PageCount.ShouldBe(112);
        record.PublishedOn.ShouldBe(new DateOnly(2023, 7, 14));
        record.Language.ShouldBe("Türkçe");
        record.Translators.ShouldBe(new[] { "Banu Gürsaler" });
        record.AuthorBios["Dag Solstad"].ShouldBe("Norveçli yazar.");
        record.CategoryPath.ShouldBe(new[] { "Edebiyat", "Roman (Çeviri)" });
        record.RelatedCategoryPaths[0].ShouldBe(new[] { "Edebiyat", "Polisiye" });
    }
}