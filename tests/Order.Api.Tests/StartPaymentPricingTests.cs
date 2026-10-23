using Order.Api.Domains.Orders.Features.Agents.Commands;
using Order.Api.Grpc;
using Shouldly;
using Xunit;

namespace Order.Api.Tests;

// 094 (İlke VI domain-TDD): start_payment tutar hesabı saf birimi — liste − aktif indirim, kuruş yuvarlama.
public class StartPaymentPricingTests
{
    [Fact]
    public void ComputeLines_NoDiscount_UsesListPrice()
    {
        var items = new List<OrderItemDto> { new(Guid.NewGuid(), "Kitap", 100m, 2) };

        var lines = StartPayment.ComputeLines(items, new Dictionary<Guid, int>());

        lines[0].UnitPrice.ShouldBe(100m);
        lines[0].Quantity.ShouldBe(2);
    }

    [Fact]
    public void ComputeLines_WithDiscount_AppliesPercentRoundedToKurus()
    {
        var pid = Guid.NewGuid();
        var items = new List<OrderItemDto> { new(pid, "Kitap", 99.99m, 1) };

        var lines = StartPayment.ComputeLines(items, new Dictionary<Guid, int> { [pid] = 20 });

        // 99.99 * 0.80 = 79.992 → 79.99 (2 ondalık, AwayFromZero)
        lines[0].UnitPrice.ShouldBe(79.99m);
    }

    [Fact]
    public void ComputeLines_UnknownProductDiscount_Ignored()
    {
        var items = new List<OrderItemDto> { new(Guid.NewGuid(), "Kitap", 50m, 1) };

        var lines = StartPayment.ComputeLines(items, new Dictionary<Guid, int> { [Guid.NewGuid()] = 50 });

        lines[0].UnitPrice.ShouldBe(50m);
    }
}
