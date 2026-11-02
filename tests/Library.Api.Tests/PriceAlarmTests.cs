namespace Library.Api.Tests;

// 060 İLKE VI: saf domain test-first — PriceAlarm.Create guard'ları.
public class PriceAlarmTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidInputs_ShouldSucceed()
    {
        var result = PriceAlarm.Create(UserId, "ayse@example.com", ProductId, "Kürk Mantolu Madonna", 149.90m);

        result.IsSuccess.ShouldBeTrue();
        var alarm = result.Data!;
        alarm.UserId.ShouldBe(UserId);
        alarm.Email.ShouldBe("ayse@example.com");
        alarm.ProductId.ShouldBe(ProductId);
        alarm.ProductName.ShouldBe("Kürk Mantolu Madonna");
        alarm.PriceAtCreation.ShouldBe(149.90m);
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldFail()
    {
        var result = PriceAlarm.Create(Guid.Empty, "ayse@example.com", ProductId, "Kitap", 10m);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == LibraryResourceConstants.PRICE_ALARM_INVALID);
    }

    [Fact]
    public void Create_WithEmptyProductId_ShouldFail()
    {
        var result = PriceAlarm.Create(UserId, "ayse@example.com", Guid.Empty, "Kitap", 10m);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == LibraryResourceConstants.PRICE_ALARM_INVALID);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositivePrice_ShouldFail(decimal price)
    {
        var result = PriceAlarm.Create(UserId, "ayse@example.com", ProductId, "Kitap", price);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == LibraryResourceConstants.PRICE_ALARM_INVALID);
    }

    // R3: email snapshot'tır, doğrulama yok — boş email alarm kurmayı ENGELLEMEZ (iz "no-email" düşer).
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyEmail_ShouldSucceedWithEmptySnapshot(string email)
    {
        var result = PriceAlarm.Create(UserId, email, ProductId, "Kitap", 10m);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Email.ShouldBe(string.Empty);
    }
}