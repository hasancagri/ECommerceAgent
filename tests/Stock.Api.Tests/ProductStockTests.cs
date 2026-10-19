namespace Stock.Api.Tests;

public class ProductStockTests
{
    [Fact]
    public void Create_SetsProductIdAndQuantity()
    {
        var productId = Guid.NewGuid();

        var stock = ProductStock.Create(productId, 10);

        stock.ProductId.ShouldBe(productId);
        stock.Quantity.ShouldBe(10);
    }

    [Fact]
    public void Increase_AddsToQuantity()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 10);

        stock.Increase(5);

        stock.Quantity.ShouldBe(15);
    }

    [Fact]
    public void Decrease_SubtractsFromQuantity()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 10);

        stock.Decrease(4);

        stock.Quantity.ShouldBe(6);
    }

    [Fact]
    public void Decrease_BelowZero_GoesNegative_NoGuard()
    {
        // Mevcut davranisi belgeler: Decrease negatif stoga izin verir (guard yok).
        // Ileride guard eklenirse bu test kirmizi bayrak olur.
        var stock = ProductStock.Create(Guid.NewGuid(), 3);

        stock.Decrease(5);

        stock.Quantity.ShouldBe(-2);
    }

    [Fact]
    public void SetQuantity_SetsAbsoluteValue()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 10);

        var result = stock.SetQuantity(42);

        result.IsSuccess.ShouldBeTrue();
        stock.Quantity.ShouldBe(42);
    }

    [Fact]
    public void SetQuantity_Zero_IsAllowed()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 10);

        var result = stock.SetQuantity(0);

        result.IsSuccess.ShouldBeTrue();
        stock.Quantity.ShouldBe(0);
    }

    [Fact]
    public void SetQuantity_Negative_ReturnsError_AndDoesNotChangeQuantity()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 10);

        var result = stock.SetQuantity(-1);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == StockResourceConstants.STOCK_QUANTITY_CANNOT_BE_NEGATIVE);
        stock.Quantity.ShouldBe(10);
    }

    // --- 056: Commit = dogrudan dusum (rezervasyon yok; stok gercegi checkout aninda) ---

    [Fact]
    public void Commit_SufficientStock_DecrementsOnHand()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 5);

        var result = stock.Commit(2, Guid.NewGuid());

        result.IsSuccess.ShouldBeTrue();
        stock.OnHand.ShouldBe(3);
    }

    [Fact]
    public void Commit_InsufficientStock_ReturnsError_AndOnHandUnchanged()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 1);

        var result = stock.Commit(2, Guid.NewGuid());

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == StockResourceConstants.STOCK_INSUFFICIENT);
        stock.OnHand.ShouldBe(1);
    }

    [Fact]
    public void Commit_ExactStock_DropsToZero_NeverNegative()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 3);

        stock.Commit(3, Guid.NewGuid()).IsSuccess.ShouldBeTrue();

        stock.OnHand.ShouldBe(0);

        // Son urun yarisi (US3): ikinci siparis ayni stoga gelir => yetersiz, eksiye inmez.
        var second = stock.Commit(1, Guid.NewGuid());
        second.IsSuccess.ShouldBeFalse();
        stock.OnHand.ShouldBe(0);
    }

    [Fact]
    public void Commit_SameOrderIdTwice_SecondIsNoOp()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 5);
        var orderId = Guid.NewGuid();

        stock.Commit(2, orderId).IsSuccess.ShouldBeTrue();
        var second = stock.Commit(2, orderId);

        second.IsSuccess.ShouldBeTrue();
        stock.OnHand.ShouldBe(3); // tek islem kadar dustu
    }

    [Fact]
    public void Commit_EmptyOrderId_ReturnsError()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 5);

        var result = stock.Commit(2, Guid.Empty);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == StockResourceConstants.STOCK_COMMIT_INVALID);
        stock.OnHand.ShouldBe(5);
    }

    [Fact]
    public void Commit_NonPositiveQuantity_ReturnsError()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 5);

        var result = stock.Commit(0, Guid.NewGuid());

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == StockResourceConstants.STOCK_COMMIT_INVALID);
        stock.OnHand.ShouldBe(5);
    }

    // 092: RevertCommit (saga telafisi) SİLİNDİ — koreografide atomik batch commit, geri alma yok.

    // --- 070: Adjust (admin artir/azalt) — tek delta metodu; negatife dusus AGGREGATE'te reddedilir ---

    [Fact]
    public void Adjust_PositiveDelta_IncreasesOnHand()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 10);

        var result = stock.Adjust(5);

        result.IsSuccess.ShouldBeTrue();
        stock.OnHand.ShouldBe(15);
    }

    [Fact]
    public void Adjust_NegativeDelta_DecreasesOnHand()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 10);

        var result = stock.Adjust(-3);

        result.IsSuccess.ShouldBeTrue();
        stock.OnHand.ShouldBe(7);
    }

    [Fact]
    public void Adjust_BelowZero_ReturnsError_AndOnHandUnchanged()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 3);

        var result = stock.Adjust(-5);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == StockResourceConstants.STOCK_QUANTITY_CANNOT_BE_NEGATIVE);
        stock.OnHand.ShouldBe(3);
    }

    [Fact]
    public void Adjust_ZeroDelta_ReturnsError()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 3);

        var result = stock.Adjust(0);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == StockResourceConstants.STOCK_ADJUST_INVALID);
        stock.OnHand.ShouldBe(3);
    }

    [Fact]
    public void Adjust_ToExactlyZero_IsAllowed()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 3);

        var result = stock.Adjust(-3);

        result.IsSuccess.ShouldBeTrue();
        stock.OnHand.ShouldBe(0);
    }
}