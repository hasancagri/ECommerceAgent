using Catalog.Api.Seeding;

namespace Catalog.Api.Tests;

// 091 T006 (İlke VI test-first): seed kaydının saf doğrulaması — satır atlatma kararı buradan çıkar
// (geçersiz satır ürün doğuramaz, seed kalanlarla sürer). Alanlar: contracts/seed-record.md.
public class SeedBookRecordTests
{
    private static SeedBookRecord NewRecord(
        string isbn = "9781442499713", decimal priceTry = 278.9m, int stock = 3) => new()
    {
        Isbn = isbn,
        Title = "Güzel Yazılar",
        Authors = ["Jenny Han"],
        Publisher = "Simon & Schuster",
        PriceTry = priceTry,
        ImageUrl = "https://covers.example/x.jpg",
        CategoryMid = "Kurgu",
        CategoryLeaf = "Çocuk Kurgu",
        Stock = stock,
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
    public void Validate_NegativeStock_ReturnsError()
    {
        var result = NewRecord(stock: -1).Validate();

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.SEED_STOCK_NEGATIVE);
    }

    [Fact]
    public void Validate_ValidRecord_Succeeds()
    {
        NewRecord().Validate().IsSuccess.ShouldBeTrue();
    }
}