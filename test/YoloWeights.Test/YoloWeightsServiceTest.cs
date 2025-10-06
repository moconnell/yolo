using Moq;
using Shouldly;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Interfaces;

namespace YoloWeights.Test;

public class YoloWeightsServiceTest
{
    [Theory]
    [InlineData(1, 1, 0.15)]
    [InlineData(0, 1, 0.2)]
    [InlineData(1, 0, 0.1)]
    [InlineData(1, 0.5, 0.1)]
    [InlineData(0.5, 1, 0.125)]
    public async Task GivenTwoFactorServices_ShouldCombineIntoSingleWeighting(
        decimal trendWeight,
        decimal retailFlowWeight,
        decimal expectedWeight,
        decimal trendValue = 0.1m,
        decimal retailFlowValue = 0.2m)
    {
        // arrange
        const string btcUsdt = "BTC/USDT";
        const int refPrice = 100000;

        var config = new YoloConfig
        {
            BaseAsset = "USDC",
            FactorWeights = new Dictionary<FactorType, decimal>
            {
                { FactorType.Trend, trendWeight },
                { FactorType.RetailFlow, retailFlowWeight }
            }
        };

        var mockFactorService1 = new Mock<IGetFactors>();
        var factorDict1 = new Dictionary<string, Dictionary<FactorType, Factor>>
        {
            {
                btcUsdt, new Dictionary<FactorType, Factor>
                {
                    {
                        FactorType.Trend,
                        new Factor("Rw.Trend", FactorType.Trend, btcUsdt, refPrice, trendValue, DateTime.UtcNow)
                    }
                }
            }
        };
        mockFactorService1.Setup(x => x.GetFactorsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(factorDict1);

        var mockFactorService2 = new Mock<IGetFactors>();
        var factorDict2 = new Dictionary<string, Dictionary<FactorType, Factor>>
        {
            {
                btcUsdt, new Dictionary<FactorType, Factor>
                {
                    {
                        FactorType.RetailFlow,
                        new Factor(
                            "Ur.RetailFlow",
                            FactorType.RetailFlow,
                            btcUsdt,
                            refPrice,
                            retailFlowValue,
                            DateTime.UtcNow)
                    }
                }
            }
        };
        mockFactorService2.Setup(x => x.GetFactorsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(factorDict2);

        var svc = new YoloWeightsService([mockFactorService1.Object, mockFactorService2.Object], config);

        // act
        var weights = await svc.CalculateWeightsAsync([btcUsdt]);

        // assert
        weights.ShouldNotBeNull();
        weights.Count.ShouldBe(1);
        weights[btcUsdt].Value.ShouldBe(expectedWeight);
    }
}