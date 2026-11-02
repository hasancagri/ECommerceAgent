namespace Order.Api.Tests;

public class OrderTests
{
    private static Address SampleAddress() =>
        new("Istanbul", "Kadikoy", "Bagdat", "34000", "No 1");

    private static OrderAggregate NewOrder() =>
        OrderAggregate.Create(Guid.NewGuid(), SampleAddress(), Guid.NewGuid());

    [Fact]
    public void Create_StartsPending_WithZeroTotalAndTenDigitCode()
    {
        var buyerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var order = OrderAggregate.Create(buyerId, SampleAddress(), paymentId);

        order.BuyerId.ShouldBe(buyerId);
        order.Status.ShouldBe(OrderStatus.Pending);
        order.PaymentId.ShouldBe(paymentId);
        order.CancelReason.ShouldBeNull();
        order.TotalPrice.ShouldBe(0m);
        order.OrderItems.ShouldBeEmpty();
        order.Code.Length.ShouldBe(10);
        order.Code.ShouldAllBe(c => char.IsDigit(c));
    }

    [Fact]
    public void AddOrderItem_EmptyProductName_ReturnsError()
    {
        var order = NewOrder();

        var result = order.AddOrderItem(Guid.NewGuid(), "", 100m);

        result.IsSuccess.ShouldBeFalse();
        order.OrderItems.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AddOrderItem_NonPositiveUnitPrice_ReturnsError(decimal unitPrice)
    {
        var order = NewOrder();

        var result = order.AddOrderItem(Guid.NewGuid(), "product", unitPrice);

        result.IsSuccess.ShouldBeFalse();
        order.OrderItems.ShouldBeEmpty();
    }

    [Fact]
    public void AddOrderItem_Valid_AddsItemAndRecalculatesTotal()
    {
        var order = NewOrder();

        var result = order.AddOrderItem(Guid.NewGuid(), "product", 120m);

        result.IsSuccess.ShouldBeTrue();
        order.OrderItems.Count.ShouldBe(1);
        order.TotalPrice.ShouldBe(120m);
    }

    [Fact]
    public void AddOrderItem_Multiple_SumsTotalPrice()
    {
        var order = NewOrder();

        order.AddOrderItem(Guid.NewGuid(), "a", 120m);
        order.AddOrderItem(Guid.NewGuid(), "b", 30m);

        order.OrderItems.Count.ShouldBe(2);
        order.TotalPrice.ShouldBe(150m);
    }

    // --- 028: durum gecisleri ---

    [Fact]
    public void Confirm_FromPending_SetsConfirmed()
    {
        var order = NewOrder();

        var result = order.Confirm();

        result.IsSuccess.ShouldBeTrue();
        order.Status.ShouldBe(OrderStatus.Confirmed);
    }

    [Fact]
    public void Cancel_FromPending_SetsCancelledWithReason()
    {
        var order = NewOrder();

        var result = order.Cancel("ORDER_TIMEOUT");

        result.IsSuccess.ShouldBeTrue();
        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.CancelReason.ShouldBe("ORDER_TIMEOUT");
    }

    [Fact]
    public void Confirm_FromCancelled_ReturnsError()
    {
        var order = NewOrder();
        order.Cancel("ORDER_TIMEOUT");

        var result = order.Confirm();

        result.IsSuccess.ShouldBeFalse();
        order.Status.ShouldBe(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromConfirmed_ReturnsError()
    {
        var order = NewOrder();
        order.Confirm();

        var result = order.Cancel("ORDER_STOCK_STEP_FAILED");

        result.IsSuccess.ShouldBeFalse();
        order.Status.ShouldBe(OrderStatus.Confirmed);
        order.CancelReason.ShouldBeNull();
    }

    [Fact]
    public void Confirm_Twice_SecondReturnsError()
    {
        var order = NewOrder();
        order.Confirm();

        var result = order.Confirm();

        result.IsSuccess.ShouldBeFalse();
        order.Status.ShouldBe(OrderStatus.Confirmed);
    }
}