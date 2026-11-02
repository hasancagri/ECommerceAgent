namespace Stock.Api.Tests;

// 002: StockItem davranış testleri — test-first (İlke VI). Envanter kurgusal; adet ≥0 invariant'ı,
// ürün başına tek kayıt (teklik DB-index'te), rezervasyon kavramı YOK (056 kararı sürer).
public class StockItemTests
{
    private static StockItem NewItem(int onHand = 50) =>
        StockItem.Create(Guid.NewGuid(), onHand).Data!;

    // --- Create ---

    [Fact]
    public void Create_ValidQuantity_SetsOnHand()
    {
        var productId = Guid.NewGuid();

        var result = StockItem.Create(productId, 50);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.ProductId.ShouldBe(productId);
        result.Data.OnHand.ShouldBe(50);
    }

    [Fact]
    public void Create_ZeroQuantity_Succeeds()
    {
        StockItem.Create(Guid.NewGuid(), 0).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_NegativeQuantity_ReturnsError()
    {
        var result = StockItem.Create(Guid.NewGuid(), -1);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == StockResourceConstants.STOCK_QUANTITY_INVALID);
    }

    [Fact]
    public void Create_EmptyProductId_ReturnsError()
    {
        var result = StockItem.Create(Guid.Empty, 10);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == StockResourceConstants.STOCK_PRODUCT_REQUIRED);
    }

    // --- SetQuantity (admin mutlak atama) ---

    [Fact]
    public void SetQuantity_Valid_ReplacesOnHand()
    {
        var item = NewItem(50);

        item.SetQuantity(20).IsSuccess.ShouldBeTrue();
        item.OnHand.ShouldBe(20);
    }

    [Fact]
    public void SetQuantity_Zero_Succeeds()
    {
        var item = NewItem(5);

        item.SetQuantity(0).IsSuccess.ShouldBeTrue();
        item.OnHand.ShouldBe(0);
    }

    [Fact]
    public void SetQuantity_Negative_ReturnsErrorAndKeepsCurrent()
    {
        var item = NewItem(50);

        var result = item.SetQuantity(-5);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == StockResourceConstants.STOCK_QUANTITY_INVALID);
        item.OnHand.ShouldBe(50);
    }

    // --- Decrease (satış düşümü) ---

    [Fact]
    public void Decrease_SufficientStock_ReducesOnHand()
    {
        var item = NewItem(3);

        item.Decrease(2).IsSuccess.ShouldBeTrue();
        item.OnHand.ShouldBe(1);
    }

    [Fact]
    public void Decrease_ToZero_Succeeds()
    {
        var item = NewItem(2);

        item.Decrease(2).IsSuccess.ShouldBeTrue();
        item.OnHand.ShouldBe(0);
    }

    [Fact]
    public void Decrease_Insufficient_ReturnsErrorAndKeepsCurrent()
    {
        var item = NewItem(3);

        var result = item.Decrease(5);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == StockResourceConstants.STOCK_INSUFFICIENT);
        item.OnHand.ShouldBe(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Decrease_NonPositiveAmount_ReturnsError(int amount)
    {
        var item = NewItem(3);

        var result = item.Decrease(amount);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == StockResourceConstants.STOCK_DECREASE_AMOUNT_INVALID);
        item.OnHand.ShouldBe(3);
    }
}
