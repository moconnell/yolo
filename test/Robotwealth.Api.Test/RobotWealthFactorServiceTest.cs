using Moq;
using Shouldly;
using RobotWealth.Api.Data;
using RobotWealth.Api.Interfaces;
using YoloAbstractions;
using YoloAbstractions.Exceptions;

namespace RobotWealth.Api.Test;

public class RobotWealthFactorServiceTest
{
    private const string BtcUsdt = "BTCUSDT";
    
    [Fact]
    public void GivenApiService_ShouldReturnFixedUniverse()
    {
        // arrange
        var mockApiSvc = new Mock<IRobotWealthApiService>();
        var svc = new RobotWealthFactorService(mockApiSvc.Object);
        
        // act
        var isFixedUniverse = svc.IsFixedUniverse;
        
        // assert
        isFixedUniverse.ShouldBeTrue();
    }

    [Fact]
    public async Task GivenApiService_WhenValidValues_ShouldReturnFactorDataFrame()
    {
        // arrange
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
                    Ticker = BtcUsdt
                }
            ]);

        mockApiService.Setup(x => x.GetVolatilitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new RwVolatility
                {
                    Date = DateTime.Today,
                    EwVol = volatilityValue,
                    Ticker = BtcUsdt
                }
            ]);

        var svc = new RobotWealthFactorService(mockApiService.Object);

        // act
        var factors = await svc.GetFactorsAsync([BtcUsdt]);

        // assert
        factors.ShouldNotBeNull();
        var btcUsdtFactors = factors[BtcUsdt];
        btcUsdtFactors.ShouldNotBeNull();
        btcUsdtFactors.Count.ShouldBe(4);
        btcUsdtFactors[FactorType.Carry].ShouldBe(carryValue);
        btcUsdtFactors[FactorType.Momentum].ShouldBe(momentumValue);
        btcUsdtFactors[FactorType.Trend].ShouldBe(trendValue);
        btcUsdtFactors[FactorType.Volatility].ShouldBe(volatilityValue);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public async Task GivenApiService_WhenNoResults_ShouldThrow(bool hasWeights, bool hasVolatilities, bool shouldThrow)
    {
        // arrange
        var mockApiService = new Mock<IRobotWealthApiService>();

        mockApiService.Setup(x => x.GetWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                hasWeights
                    ?
                    [
                        new RwWeight
                        {
                            ArrivalPrice = 100000,
                            CarryMegafactor = 1,
                            MomentumMegafactor = 2,
                            TrendMegafactor = 3,
                            Date = DateTime.Today,
                            Ticker = BtcUsdt
                        }
                    ]
                    : []);

        mockApiService.Setup(x => x.GetVolatilitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                hasVolatilities
                    ?
                    [
                        new RwVolatility
                        {
                            Date = DateTime.Today,
                            EwVol = 4,
                            Ticker = BtcUsdt
                        }
                    ]
                    : []);

        var svc = new RobotWealthFactorService(mockApiService.Object);

        // act
        var f = () => svc.GetFactorsAsync();

        // assert
        if (shouldThrow)
        {
            await f.ShouldThrowAsync<ApiException>();
        }
        else
        {
            var res = await f();
            res.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task GivenApiService_WhenTickerMismatch_ShouldThrow()
    {
        // arrange
        var mockApiService = new Mock<IRobotWealthApiService>();

        mockApiService.Setup(x => x.GetWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new RwWeight
                {
                    ArrivalPrice = 100000,
                    CarryMegafactor = 1,
                    MomentumMegafactor = 2,
                    TrendMegafactor = 3,
                    Date = DateTime.Today,
                    Ticker = BtcUsdt
                }
            ]);

        mockApiService.Setup(x => x.GetVolatilitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new RwVolatility
                {
                    Date = DateTime.Today,
                    EwVol = 4,
                    Ticker = "ETHUSDT"
                }
            ]);

        var svc = new RobotWealthFactorService(mockApiService.Object);

        // act
        var f = () => svc.GetFactorsAsync();

        // assert
        await f.ShouldThrowAsync<ApiException>();
    }
}