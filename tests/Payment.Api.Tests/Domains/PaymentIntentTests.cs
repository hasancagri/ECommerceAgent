namespace Payment.Api.Tests.Domains;

// 077 (İLKE VI, Domain-TDD): PaymentIntent aggregate davranış testleri — impl'den ÖNCE yazıldı.
// Kapsam: Create guard, MarkSucceeded idempotent, MarkFailed/Expire yalnız Pending'den + terminal reddi,
// IsLive canlı/bayat. Saf domain (PSP/Marten yok); "now" parametre olarak geçer (test-edilebilirlik).
public class PaymentIntentTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string BasketRef = "basket-hash-1";
    private const string TxRef = "tx-123";
    private const string HostedUrl = "https://pg.example/hosted/abc";
    private const string PgRef = "pg-ref-1";

    private static PaymentIntent NewPending() =>
        PaymentIntent.Create(OrderId, UserId, BasketRef, 250m, TxRef, PgRef, HostedUrl).Data!;

    // ---- Create guard ----

    [Fact]
    public void Create_Valid_ReturnsPending()
    {
        var result = PaymentIntent.Create(OrderId, UserId, BasketRef, 250m, TxRef, PgRef, HostedUrl);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Status.ShouldBe(PaymentIntentStatus.Pending);
        result.Data!.OrderId.ShouldBe(OrderId);
        result.Data!.UserId.ShouldBe(UserId);
        result.Data!.TxRef.ShouldBe(TxRef);
        result.Data!.HostedUrl.ShouldBe(HostedUrl);
        result.Data!.Amount.ShouldBe(250m);
    }

    [Fact]
    public void Create_EmptyOrderId_ReturnsError()
    {
        var result = PaymentIntent.Create(Guid.Empty, UserId, BasketRef, 250m, TxRef, PgRef, HostedUrl);
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Create_EmptyUserId_ReturnsError()
    {
        var result = PaymentIntent.Create(OrderId, Guid.Empty, BasketRef, 250m, TxRef, PgRef, HostedUrl);
        result.IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_NonPositiveAmount_ReturnsError(decimal amount)
    {
        var result = PaymentIntent.Create(OrderId, UserId, BasketRef, amount, TxRef, PgRef, HostedUrl);
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Create_EmptyTxRef_ReturnsError()
    {
        var result = PaymentIntent.Create(OrderId, UserId, BasketRef, 250m, "", PgRef, HostedUrl);
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Create_EmptyHostedUrl_ReturnsError()
    {
        var result = PaymentIntent.Create(OrderId, UserId, BasketRef, 250m, TxRef, PgRef, "");
        result.IsSuccess.ShouldBeFalse();
    }

    // ---- MarkSucceeded (idempotent, terminal reddi) ----

    [Fact]
    public void MarkSucceeded_FromPending_Succeeds()
    {
        var intent = NewPending();

        var result = intent.MarkSucceeded("pg-final");

        result.IsSuccess.ShouldBeTrue();
        intent.Status.ShouldBe(PaymentIntentStatus.Succeeded);
        intent.PgPaymentRef.ShouldBe("pg-final");
    }

    [Fact]
    public void MarkSucceeded_Twice_IsNoOpOk()
    {
        var intent = NewPending();
        intent.MarkSucceeded("pg-final");

        var second = intent.MarkSucceeded("pg-final");

        second.IsSuccess.ShouldBeTrue(); // çift callback → no-op Ok
        intent.Status.ShouldBe(PaymentIntentStatus.Succeeded);
    }

    [Fact]
    public void MarkSucceeded_FromFailed_ReturnsError()
    {
        var intent = NewPending();
        intent.MarkFailed("DECLINED");

        var result = intent.MarkSucceeded("pg-final");

        result.IsSuccess.ShouldBeFalse();
        intent.Status.ShouldBe(PaymentIntentStatus.Failed);
    }

    [Fact]
    public void MarkSucceeded_FromExpired_ReturnsError()
    {
        var intent = NewPending();
        intent.Expire();

        var result = intent.MarkSucceeded("pg-final");

        result.IsSuccess.ShouldBeFalse();
        intent.Status.ShouldBe(PaymentIntentStatus.Expired);
    }

    // ---- MarkFailed (yalnız Pending; Succeeded reddi; Failed/Expired no-op) ----

    [Fact]
    public void MarkFailed_FromPending_Fails()
    {
        var intent = NewPending();

        var result = intent.MarkFailed("DECLINED");

        result.IsSuccess.ShouldBeTrue();
        intent.Status.ShouldBe(PaymentIntentStatus.Failed);
        intent.FailureReason.ShouldBe("DECLINED");
    }

    [Fact]
    public void MarkFailed_FromSucceeded_ReturnsError()
    {
        var intent = NewPending();
        intent.MarkSucceeded("pg-final");

        var result = intent.MarkFailed("DECLINED");

        result.IsSuccess.ShouldBeFalse(); // para alındı → başarısıza çekilemez
        intent.Status.ShouldBe(PaymentIntentStatus.Succeeded);
    }

    [Fact]
    public void MarkFailed_Twice_IsNoOpOk()
    {
        var intent = NewPending();
        intent.MarkFailed("DECLINED");

        var second = intent.MarkFailed("DECLINED");

        second.IsSuccess.ShouldBeTrue();
        intent.Status.ShouldBe(PaymentIntentStatus.Failed);
    }

    // ---- Expire (yalnız Pending; diğer no-op) ----

    [Fact]
    public void Expire_FromPending_Expires()
    {
        var intent = NewPending();

        var result = intent.Expire();

        result.IsSuccess.ShouldBeTrue();
        intent.Status.ShouldBe(PaymentIntentStatus.Expired);
        intent.FailureReason.ShouldBe("ABANDONED");
    }

    [Fact]
    public void Expire_FromSucceeded_IsNoOpOk_StaysSucceeded()
    {
        var intent = NewPending();
        intent.MarkSucceeded("pg-final");

        var result = intent.Expire();

        result.IsSuccess.ShouldBeTrue(); // timer geç geldi → no-op
        intent.Status.ShouldBe(PaymentIntentStatus.Succeeded);
    }

    // ---- IsLive (canlı/bayat) ----

    [Fact]
    public void IsLive_PendingWithinTimeout_True()
    {
        var intent = NewPending();

        intent.IsLive(300, intent.CreatedTime.AddSeconds(10)).ShouldBeTrue();
    }

    [Fact]
    public void IsLive_PendingPastTimeout_False()
    {
        var intent = NewPending();

        intent.IsLive(300, intent.CreatedTime.AddSeconds(400)).ShouldBeFalse();
    }

    [Fact]
    public void IsLive_NotPending_False()
    {
        var intent = NewPending();
        intent.MarkSucceeded("pg-final");

        intent.IsLive(300, intent.CreatedTime.AddSeconds(10)).ShouldBeFalse();
    }
}
