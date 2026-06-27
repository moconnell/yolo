using RobotWealth.Api.Data;
using YoloAbstractions;

namespace RobotWealth.Api.Test;

public class RobotWealthFactorTypeMapperTest
{
    [Theory]
    [InlineData(RobotWealthFactorType.CarryMegafactor, FactorType.Carry, 1)]
    [InlineData(RobotWealthFactorType.MomentumMegafactor, FactorType.Momentum, 2)]
    [InlineData(RobotWealthFactorType.TrendMegafactor, FactorType.Trend, 3)]
    [InlineData(RobotWealthFactorType.ExponentiallyWeightedVolatility, FactorType.Volatility, 4)]
    public void GivenRobotWealthFactorType_WhenMapped_ShouldReturnCommonFactorTypeAndValue(
        RobotWealthFactorType factorType,
        FactorType expectedCommonFactorType,
        double expectedValue)
    {
        var weight = new RwWeight
        {
            Date = DateTime.Today,
            Ticker = "BTCUSDT",
            CarryMegafactor = 1,
            MomentumMegafactor = 2,
            TrendMegafactor = 3
        };
        var volatility = new RwVolatility
        {
            Date = DateTime.Today,
            Ticker = "BTCUSDT",
            EwVol = 4
        };

        factorType.ToFactorType().ShouldBe(expectedCommonFactorType);
        factorType.GetValue(weight, volatility).ShouldBe(expectedValue);
    }

    [Theory]
    [InlineData(RobotWealthFactorType.Unknown)]
    [InlineData((RobotWealthFactorType)999)]
    public void GivenUnsupportedRobotWealthFactorType_WhenMapped_ShouldThrow(RobotWealthFactorType factorType)
    {
        var weight = new RwWeight { Date = DateTime.Today, Ticker = "BTCUSDT" };
        var volatility = new RwVolatility { Date = DateTime.Today, Ticker = "BTCUSDT" };

        Should.Throw<InvalidOperationException>(() => factorType.ToFactorType());
        Should.Throw<InvalidOperationException>(() => factorType.GetValue(weight, volatility));
    }
}
