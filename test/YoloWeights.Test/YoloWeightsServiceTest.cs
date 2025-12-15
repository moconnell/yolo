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

        var testDate = new DateTime(2025, 10, 13, 0, 0, 0, DateTimeKind.Utc);

        var mockFactorService1 = new Mock<IGetFactors>();
        mockFactorService1.Setup(x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FactorDataFrame.NewFrom([btcUsdt], testDate, (Trend, [Convert.ToDouble(trendValue)])));

        var mockFactorService2 = new Mock<IGetFactors>();
        mockFactorService2.Setup(x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                FactorDataFrame.NewFrom([btcUsdt], testDate, (RetailFlow, [Convert.ToDouble(retailFlowValue)])));

        var logger = _loggerFactory.CreateLogger<YoloWeightsService>();

        var svc = new YoloWeightsService(
            [mockFactorService1.Object, mockFactorService2.Object],
            config,
            logger);

        // act
        var weights = await svc.CalculateWeightsAsync();

        // assert
        weights.ShouldNotBeNull();
        weights.Count.ShouldBe(1);
        weights[btcUsdt].ShouldBe(expectedWeight);
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

    [Fact]
    public async Task GivenMaxWeightingAbs_WhenCalculatingWeights_ShouldClampWeights()
    {
        // arrange
        const string btcUsdt = "BTC/USDT";
        const string ethUsdt = "ETH/USDT";

        var config = new YoloConfig
        {
            BaseAsset = "USDC",
            FactorWeights = new Dictionary<FactorType, decimal> { { Carry, 1m } },
            MaxWeightingAbs = 0.25
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
                    (Carry, [1.0, 0.1]))); // Large difference to trigger capping

        var logger = _loggerFactory.CreateLogger<YoloWeightsService>();
        var svc = new YoloWeightsService([mockFactorService.Object], config, logger);

        // act
        var weights = await svc.CalculateWeightsAsync();

        // assert
        weights.ShouldNotBeNull();
        weights.Values.All(w => Math.Abs(w) <= 0.25m).ShouldBeTrue();
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
            FactorNormalizationMethod = NormalizationMethod.MinMax
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

    [Fact]
    public async Task GivenZeroFactorWeight_WhenCalculating_ShouldHandleCorrectly()
    {
        // arrange
        const string btcUsdt = "BTC/USDT";

        var config = new YoloConfig
        {
            BaseAsset = "USDC",
            FactorWeights = new Dictionary<FactorType, decimal>
            {
                { Carry, 0m },
                { Momentum, 1m }
            }
        };

        var mockFactorService = new Mock<IGetFactors>();
        mockFactorService.Setup(x => x.GetFactorsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ISet<FactorType>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                FactorDataFrame.NewFrom(
                    [btcUsdt],
                    DateTime.Today,
                    (Carry, [0.5]),
                    (Momentum, [0.3])));

        var logger = _loggerFactory.CreateLogger<YoloWeightsService>();
        var svc = new YoloWeightsService([mockFactorService.Object], config, logger);

        // act
        var weights = await svc.CalculateWeightsAsync();

        // assert
        weights.ShouldNotBeNull();
        weights[btcUsdt].ShouldBe(0.3m);
    }
}