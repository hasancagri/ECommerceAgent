namespace Customer.Api.Tests;

public class MerchantInformationTests
{
    private static readonly Guid ValidMerchant = Guid.NewGuid();
    private const string ValidKey = "mk_abc123";

    [Fact]
    public void Create_ValidInput_ReturnsOk_AndActive()
    {
        var result = MerchantInformation.Create(ValidMerchant, ValidKey);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.MerchantId.ShouldBe(ValidMerchant);
        result.Data.MerchantKey.ShouldBe(ValidKey);
        result.Data.Status.ShouldBe("Active");
    }

    [Fact]
    public void Create_WhitespacePaddedKey_TrimsKey()
    {
        var result = MerchantInformation.Create(ValidMerchant, "  mk_x  ");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.MerchantKey.ShouldBe("mk_x");
    }

    [Fact]
    public void Create_EmptyMerchantId_ReturnsError()
    {
        var result = MerchantInformation.Create(Guid.Empty, ValidKey);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Create_EmptyKey_ReturnsError()
    {
        var result = MerchantInformation.Create(ValidMerchant, "   ");

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void UpdateKey_ValidKey_UpdatesKey()
    {
        var info = MerchantInformation.Create(ValidMerchant, ValidKey).Data!;

        var result = info.UpdateKey("mk_new");

        result.IsSuccess.ShouldBeTrue();
        info.MerchantKey.ShouldBe("mk_new");
        info.MerchantId.ShouldBe(ValidMerchant); // kimlik değişmez
    }

    [Fact]
    public void UpdateKey_EmptyKey_ReturnsError_AndPreservesOldKey()
    {
        var info = MerchantInformation.Create(ValidMerchant, ValidKey).Data!;

        var result = info.UpdateKey("  ");

        result.IsSuccess.ShouldBeFalse();
        info.MerchantKey.ShouldBe(ValidKey);
    }

    // --- 087: makine-handoff kayıt + HMAC-callback credential set ---

    [Fact]
    public void StartRegistration_Pending_ve_correlation_set()
    {
        var correlation = Guid.NewGuid();
        var info = MerchantInformation.NewUnregistered();

        var result = info.StartRegistration(correlation);

        result.IsSuccess.ShouldBeTrue();
        info.Registration.ShouldBe(RegistrationStatus.Pending);
        info.PendingCorrelationId.ShouldBe(correlation);
    }

    [Fact]
    public void StartRegistration_EmptyCorrelation_ReturnsError()
    {
        var info = MerchantInformation.NewUnregistered();

        var result = info.StartRegistration(Guid.Empty);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void StartRegistration_WhenPendingWithDifferentCorrelation_ReturnsError()
    {
        var info = MerchantInformation.NewUnregistered();
        info.StartRegistration(Guid.NewGuid());

        var result = info.StartRegistration(Guid.NewGuid());

        result.IsSuccess.ShouldBeFalse(); // tek-aktif kayıt guard
    }

    [Fact]
    public void StartRegistration_WhenPendingWithSameCorrelation_IsIdempotentOk()
    {
        var correlation = Guid.NewGuid();
        var info = MerchantInformation.NewUnregistered();
        info.StartRegistration(correlation);

        var result = info.StartRegistration(correlation);

        result.IsSuccess.ShouldBeTrue();
        info.PendingCorrelationId.ShouldBe(correlation);
    }

    [Fact]
    public void ApplyCredentialsFromCallback_MatchingCorrelation_SetsActive_AndKey()
    {
        var correlation = Guid.NewGuid();
        var info = MerchantInformation.NewUnregistered();
        info.StartRegistration(correlation);

        var result = info.ApplyCredentialsFromCallback(correlation, ValidMerchant, ValidKey);

        result.IsSuccess.ShouldBeTrue();
        info.Registration.ShouldBe(RegistrationStatus.Active);
        info.MerchantId.ShouldBe(ValidMerchant);
        info.MerchantKey.ShouldBe(ValidKey);
        info.CredentialsVerified.ShouldBeTrue();
    }

    [Fact]
    public void ApplyCredentialsFromCallback_NonMatchingCorrelation_ReturnsError_AndDoesNotPersist()
    {
        var info = MerchantInformation.NewUnregistered();
        info.StartRegistration(Guid.NewGuid());

        var result = info.ApplyCredentialsFromCallback(Guid.NewGuid(), ValidMerchant, ValidKey);

        result.IsSuccess.ShouldBeFalse();
        info.Registration.ShouldBe(RegistrationStatus.Pending);
        info.MerchantKey.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyCredentialsFromCallback_DoubleCallbackSameCorrelation_IsIdempotentNoOp()
    {
        var correlation = Guid.NewGuid();
        var info = MerchantInformation.NewUnregistered();
        info.StartRegistration(correlation);
        info.ApplyCredentialsFromCallback(correlation, ValidMerchant, ValidKey);

        // İkinci kez aynı callback: no-op Ok, key değişmez (farklı key denense bile yazmaz).
        var result = info.ApplyCredentialsFromCallback(correlation, Guid.NewGuid(), "mk_other");

        result.IsSuccess.ShouldBeTrue();
        info.MerchantId.ShouldBe(ValidMerchant);
        info.MerchantKey.ShouldBe(ValidKey);
    }
}
