using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Interfaces;
using YoloTest.Util;
using static YoloAbstractions.FactorType;

namespace YoloWeights.Test;

public class YoloWeightsServiceTest
{
    private readonly ILoggerFactory _loggerFactory;

    public YoloWeightsServiceTest(ITestOutputHelper outputHelper)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XUnitLoggerProvider(outputHelper));
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }

    [Fact]
    public void GivenNullInner_WhenConstructing_ShouldThrowArgumentNullException()
    {
        // arrange
        var config = new YoloConfig
        {
            BaseAsset = "USDC",
            FactorWeights = new Dictionary<FactorType, decimal> { { Carry, 1m } }
        };
        var logger = _loggerFactory.CreateLogger<YoloWeightsService>();

        // act & assert
        Should.Throw<ArgumentNullException>(() =>
            new YoloWeightsService(null!, config, logger));
    }

    [Fact]
    public void GivenNullConfig_WhenConstructing_ShouldThrowArgumentNullException()
    {
        // arrange
        var mockFactorService = new Mock<IGetFactors>();
        var logger = _loggerFactory.CreateLogger<YoloWeightsService>();

        // act & assert
        Should.Throw<ArgumentNullException>(() =>
            new YoloWeightsService([mockFactorService.Object], null!, logger));
    }

    [Fact]
    public void GivenEmptyInner_WhenConstructing_ShouldThrowArgumentException()
    {
        // arrange
        var config = new YoloConfig
        {
            BaseAsset = "USDC",
            FactorWeights = new Dictionary<FactorType, decimal> { { Carry, 1m } }
        };
        var logger = _loggerFactory.CreateLogger<YoloWeightsService>();

        // act & assert
        var exception = Should.Throw<ArgumentException>(() =>
            new YoloWeightsService([], config, logger));
        exception.Message.ShouldContain("must have at least one factor provider");
    }

    [Theory]
    [InlineData(1, 0.1, null, 0.909090909090909, 0.0909090909090909)] // No capping, weights should reflect relative factor values
    [InlineData(1, 0.1, 0.25, 0.7142857142857143, 0.28571428571428575)] // Large difference to trigger capping
    public async Task GivenMaxWeightingAbs_WhenCalculatingWeights_ShouldClampWeights(double carryBtcUsdt, double carryEthUsdt, double? maxWeightingAbs = null, double expectedBtcUsdt = 0.25, double expectedEthUsdt = 0.25)
    {
        // arrange
        const string btcUsdt = "BTC/USDT";
        const string ethUsdt = "ETH/USDT";

        var config = new YoloConfig
        {
            BaseAsset = "USDC",
            FactorWeights = new Dictionary<FactorType, decimal> { { Carry, 1m } },
            MaxWeightingAbs = maxWeightingAbs
        };

        var mockFactorService = new Mock<IGetFactors>();
        mockFactorService.Setup(x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                FactorDataFrame.NewFrom(
                    [btcUsdt, ethUsdt],
                    DateTime.Today,
                    (Carry, [carryBtcUsdt, carryEthUsdt])));

        var logger = _loggerFactory.CreateLogger<YoloWeightsService>();
        var svc = new YoloWeightsService([mockFactorService.Object], config, logger);

        // act
        var weights = await svc.CalculateWeightsAsync();

        // assert
        weights.ShouldNotBeNull();
        weights.Count.ShouldBe(2);
        weights[btcUsdt].ShouldBe((decimal)expectedBtcUsdt);
        weights[ethUsdt].ShouldBe((decimal)expectedEthUsdt);
    }

    [Fact]
    public async Task GivenNormalizationMethod_WhenCalculatingWeights_ShouldNormalizeFactors()
    {
        // arrange
        const string btcUsdt = "BTC/USDT";
        const string ethUsdt = "ETH/USDT";

        var config = new YoloConfig
        {
            BaseAsset = "USDC",
            FactorWeights = new Dictionary<FactorType, decimal> { { Carry, 1m } },
            NormalizationMethod = NormalizationMethod.CrossSectionalBins,
            QuantilesForNormalization = 2 // Simple binary split for test
        };

        var mockFactorService = new Mock<IGetFactors>();
        mockFactorService.Setup(x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                FactorDataFrame.NewFrom(
                    [btcUsdt, ethUsdt],
                    DateTime.Today,
                    (Carry, [0.1, 0.9])));

        var logger = _loggerFactory.CreateLogger<YoloWeightsService>();
        var svc = new YoloWeightsService([mockFactorService.Object], config, logger);

        // act
        var weights = await svc.CalculateWeightsAsync();

        // assert
        weights.ShouldNotBeNull();
        weights.Count.ShouldBe(2);
        weights[btcUsdt].ShouldBe(-0.5m); // Lower value should get negative weight
        weights[ethUsdt].ShouldBe(0.5m);  // Higher value
    }

    [Fact]
    public async Task GivenMultipleServices_WhenCalculatingWeights_ShouldAggregateFactors()
    {
        // arrange
        const string btcUsdt = "BTC/USDT";
        const string ethUsdt = "ETH/USDT";

        var config = new YoloConfig
        {
            BaseAsset = "USDC",
            FactorWeights = new Dictionary<FactorType, decimal>
            {
                { Carry, 1m },
                { Momentum, 1m },
                { Trend, 1m }
            }
        };

        var mockService1 = new Mock<IGetFactors>();
        mockService1.Setup(x => x.IsFixedUniverse).Returns(true);
        mockService1.Setup(x => x.Order).Returns(0);
        mockService1.Setup(x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                FactorDataFrame.NewFrom(
                    [btcUsdt, ethUsdt],
                    DateTime.Today,
                    (Carry, [0.1, 0.2])));

        var mockService2 = new Mock<IGetFactors>();
        mockService2.Setup(x => x.IsFixedUniverse).Returns(false);
        mockService2.Setup(x => x.Order).Returns(1);
        mockService2.Setup(x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                FactorDataFrame.NewFrom(
                    [btcUsdt, ethUsdt],
                    DateTime.Today,
                    (Momentum, [0.3, 0.4])));

        var mockService3 = new Mock<IGetFactors>();
        mockService3.Setup(x => x.IsFixedUniverse).Returns(false);
        mockService3.Setup(x => x.Order).Returns(2);
        mockService3.Setup(x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                FactorDataFrame.NewFrom(
                    [btcUsdt, ethUsdt],
                    DateTime.Today,
                    (Trend, [0.5, 0.6])));

        var logger = _loggerFactory.CreateLogger<YoloWeightsService>();
        var svc = new YoloWeightsService(
            [mockService1.Object, mockService2.Object, mockService3.Object],
            config,
            logger);

        // act
        var weights = await svc.CalculateWeightsAsync();

        // assert
        weights.ShouldNotBeNull();
        weights.Count.ShouldBe(2);
        // Verify all services were called
        mockService1.Verify(
            x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        mockService2.Verify(
            x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        mockService3.Verify(
            x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GivenServicesWithDifferentOrders_WhenCalculatingWeights_ShouldProcessInCorrectOrder()
    {
        // arrange
        const string btcUsdt = "BTC/USDT";
        var processOrder = new List<int>();

        var config = new YoloConfig
        {
            BaseAsset = "USDC",
            FactorWeights = new Dictionary<FactorType, decimal>
            {
                { Carry, 1m },
                { Momentum, 1m }
            }
        };

        var mockService1 = new Mock<IGetFactors>();
        mockService1.Setup(x => x.IsFixedUniverse).Returns(false);
        mockService1.Setup(x => x.Order).Returns(2);
        mockService1.Setup(x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => processOrder.Add(2))
            .ReturnsAsync(FactorDataFrame.NewFrom([btcUsdt], DateTime.Today, (Carry, [0.1])));

        var mockService2 = new Mock<IGetFactors>();
        mockService2.Setup(x => x.IsFixedUniverse).Returns(true);
        mockService2.Setup(x => x.Order).Returns(1);
        mockService2.Setup(x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => processOrder.Add(1))
            .ReturnsAsync(FactorDataFrame.NewFrom([btcUsdt], DateTime.Today, (Momentum, [0.2])));

        var logger = _loggerFactory.CreateLogger<YoloWeightsService>();
        var svc = new YoloWeightsService([mockService1.Object, mockService2.Object], config, logger);

        // act
        await svc.CalculateWeightsAsync();

        // assert
        // IsFixedUniverse=true should come first, then ordered by Order
        processOrder.ShouldBe([1, 2]);
    }

    [Fact]
    public async Task GivenCancellationToken_WhenCalculating_ShouldPassThroughToServices()
    {
        // arrange
        const string btcUsdt = "BTC/USDT";
        var cts = new CancellationTokenSource();

        var config = new YoloConfig
        {
            BaseAsset = "USDC",
            FactorWeights = new Dictionary<FactorType, decimal> { { Carry, 1m } }
        };

        var mockFactorService = new Mock<IGetFactors>();
        mockFactorService.Setup(x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.Is<CancellationToken>(ct => ct == cts.Token)))
            .ReturnsAsync(FactorDataFrame.NewFrom([btcUsdt], DateTime.Today, (Carry, [0.1])));

        var logger = _loggerFactory.CreateLogger<YoloWeightsService>();
        var svc = new YoloWeightsService([mockFactorService.Object], config, logger);

        // act
        await svc.CalculateWeightsAsync(cts.Token);

        // assert
        mockFactorService.Verify(
            x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task GivenServicesWithDifferingBaseAssets_WhenAggregating_ShouldThrow()
    {
        // arrange
        const string btcUsdt = "BTC/USDT";
        const string ethUsdt = "ETH/USDT";
        const string adaUsdt = "ADA/USDT";

        var config = new YoloConfig
        {
            BaseAsset = "USDC",
            FactorWeights = new Dictionary<FactorType, decimal>
            {
                { Carry, 1m },
                { Momentum, 1m }
            }
        };

        HashSet<string>? capturedBaseAssets = null;

        var mockService1 = new Mock<IGetFactors>();
        mockService1.Setup(x => x.IsFixedUniverse).Returns(true);
        mockService1.Setup(x => x.Order).Returns(0);
        mockService1.Setup(x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                FactorDataFrame.NewFrom(
                    [btcUsdt, ethUsdt],
                    DateTime.Today,
                    (Carry, [0.1, 0.2])));

        var mockService2 = new Mock<IGetFactors>();
        mockService2.Setup(x => x.IsFixedUniverse).Returns(false);
        mockService2.Setup(x => x.Order).Returns(1);
        mockService2.Setup(x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<string>, ISet<FactorType>, CancellationToken>((baseAssets, _, _) =>
                capturedBaseAssets = baseAssets.ToHashSet())
            .ReturnsAsync(
                FactorDataFrame.NewFrom(
                    [btcUsdt, ethUsdt, adaUsdt],
                    DateTime.Today,
                    (Momentum, [0.3, 0.4, 0.5])));

        var logger = _loggerFactory.CreateLogger<YoloWeightsService>();
        var svc = new YoloWeightsService([mockService1.Object, mockService2.Object], config, logger);

        // act
        var func = () => svc.CalculateWeightsAsync();

        // assert
        await Assert.ThrowsAsync<ArgumentException>(func);
    }
}