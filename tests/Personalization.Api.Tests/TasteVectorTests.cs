using Personalization.Api.Domains.Taste;
using Shouldly;
using Xunit;

namespace Personalization.Api.Tests;

// 028 Dilim-4 (İlke VI test-first): taste-vektör merkez-hesabı + cold-start eşiği SAF birimler.
public class TasteVectorTests
{
    [Fact]
    public void Average_Empty_ReturnsEmpty()
    {
        TasteVector.Average([]).ShouldBeEmpty();
    }

    [Fact]
    public void Average_Single_ReturnsSame()
    {
        TasteVector.Average([[1f, 2f, 3f]]).ShouldBe([1f, 2f, 3f]);
    }

    [Fact]
    public void Average_Multiple_DimensionWiseMean()
    {
        TasteVector.Average([[1f, 2f], [3f, 4f]]).ShouldBe([2f, 3f]);
    }

    [Fact]
    public void Average_WithWeights_WeightedMean()
    {
        // (0*1 + 10*3)/(1+3) = 7.5
        TasteVector.Average([[0f, 0f], [10f, 10f]], [1f, 3f]).ShouldBe([7.5f, 7.5f]);
    }

    [Fact]
    public void IsColdStart_BelowThreshold_True()
    {
        TasteVector.IsColdStart(0).ShouldBeTrue();
        TasteVector.IsColdStart(2).ShouldBeTrue();
    }

    [Fact]
    public void IsColdStart_AtOrAboveThreshold_False()
    {
        TasteVector.IsColdStart(3).ShouldBeFalse();
        TasteVector.IsColdStart(10).ShouldBeFalse();
    }
}
