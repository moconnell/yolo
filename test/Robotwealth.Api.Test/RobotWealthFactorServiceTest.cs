using System.Net;
using Moq;
using Moq.Contrib.HttpClient;
using RobotWealth.Api;
using Shouldly;
using RobotWealth.Api.Config;
using RobotWealth.Api.Data;
using RobotWealth.Api.Interfaces;
using YoloAbstractions;

namespace Robotwealth.Api.Test;

public class RobotWealthFactorServiceTest
{
    [Fact]
    public async Task GivenGoodConfig_ShouldReturnFactors()
    {
        // arrange
        const string btcUsdt = "BTCUSDT";
        const double carryValue = -0.116056661080926;
        const double momentumValue = 0.129485645933014;
        const double trendValue = 0.0853393962230077;
        const double volatilityValue = 0.272582342594688;

        var mockApiService = new Mock<IRobotWealthApiService>();

        mockApiService.Setup(x => x.GetWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new RwWeight
                {
                    ArrivalPrice = 100000,
                    CarryMegafactor = carryValue,
                    MomentumMegafactor = momentumValue,
                    TrendMegafactor = trendValue,
                    Date = DateTime.Today,
                    Ticker = btcUsdt
                }
            ]);

        mockApiService.Setup(x => x.GetVolatilitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new RwVolatility
                {
                    Date = DateTime.Today,
                    EwVol = volatilityValue,
                    Ticker = btcUsdt
                }
            ]);

        var svc = new RobotWealthFactorService(mockApiService.Object);

        // act
        var factors = await svc.GetFactorsAsync([btcUsdt]);

        // assert
        factors.ShouldNotBeNull();
        var btcUsdtFactors = factors[btcUsdt];
        btcUsdtFactors.ShouldNotBeNull();
        btcUsdtFactors.Count.ShouldBe(4);
        btcUsdtFactors[FactorType.Carry].ShouldBe(carryValue);
        btcUsdtFactors[FactorType.Momentum].ShouldBe(momentumValue);
        btcUsdtFactors[FactorType.Trend].ShouldBe(trendValue);
        btcUsdtFactors[FactorType.Volatility].ShouldBe(volatilityValue);
    }
}