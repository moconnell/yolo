using YoloAbstractions;

namespace Unravel.Api.Test;

public class UnravelFactorTypeMapperTest
{
    [Theory]
    [InlineData(UnravelFactorType.InstantaneousMomentum, "instantaneous_momentum", FactorType.InstantaneousMomentum)]
    [InlineData(UnravelFactorType.InstantaneousVolatility, "instantaneous_volatility", FactorType.InstantaneousVolatility)]
    [InlineData(UnravelFactorType.EnhancedMomentum, "momentum_enhanced", FactorType.EnhancedMomentum)]
    [InlineData(UnravelFactorType.EnhancedMeanReversion, "mean_reversion_enhanced", FactorType.EnhancedMeanReversion)]
    public void GivenUnravelFactorType_WhenMapped_ShouldReturnApiNameAndCommonFactorType(
        UnravelFactorType factorType,
        string expectedApiName,
        FactorType expectedCommonFactorType)
    {
        factorType.ToApiName().ShouldBe(expectedApiName);
        factorType.ToFactorType().ShouldBe(expectedCommonFactorType);
    }

    [Theory]
    [InlineData(UnravelFactorType.Unknown)]
    [InlineData((UnravelFactorType)999)]
    public void GivenUnsupportedUnravelFactorType_WhenMapped_ShouldThrow(UnravelFactorType factorType)
    {
        Should.Throw<InvalidOperationException>(() => factorType.ToApiName());
        Should.Throw<InvalidOperationException>(() => factorType.ToFactorType());
    }
}
