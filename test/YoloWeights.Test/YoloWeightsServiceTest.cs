using Moq;
using Shouldly;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Interfaces;
using static YoloAbstractions.FactorType;

namespace YoloWeights.Test;

public class YoloWeightsServiceTest
{
    [Theory]
    [InlineData(1, 1, 0.15)]
    [InlineData(0, 1, 0.2)]
    [InlineData(1, 0, 0.1)]
    [InlineData(1, 0.5, 0.133333333333333)]
    [InlineData(0.5, 1, 0.166666666666667)]
    public async Task GivenTwoFactorServices_ShouldCombineIntoSingleWeighting(
        decimal trendWeight,
        decimal retailFlowWeight,
        decimal expectedWeight,
        decimal trendValue = 0.1m,
        decimal retailFlowValue = 0.2m)
    {
        // arrange
        const string btcUsdt = "BTC/USDT";

        var config = new YoloConfig
        {
            BaseAsset = "USDC",
            FactorWeights = new Dictionary<FactorType, decimal>
            {
                { Trend, trendWeight },
                { RetailFlow, retailFlowWeight }
            }
        };

        var mockFactorService1 = new Mock<IGetFactors>();
        mockFactorService1.Setup(x => x.GetFactorsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FactorDataFrame.NewFrom([btcUsdt], DateTime.Today, (Trend, [Convert.ToDouble(trendValue)])));

        var mockFactorService2 = new Mock<IGetFactors>();
        mockFactorService2.Setup(x => x.GetFactorsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                FactorDataFrame.NewFrom([btcUsdt], DateTime.Today, (RetailFlow, [Convert.ToDouble(retailFlowValue)])));

        var svc = new YoloWeightsService([mockFactorService1.Object, mockFactorService2.Object], config);

        // act
        var weights = await svc.CalculateWeightsAsync([btcUsdt]);

        // assert
        weights.ShouldNotBeNull();
        weights.Count.ShouldBe(1);
        weights[btcUsdt].ShouldBe(expectedWeight);
    }
}