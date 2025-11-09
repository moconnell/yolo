using Moq;
using Shouldly;
using YoloAbstractions;
using YoloBroker.Interface;

namespace YoloBroker.Test;

public class BrokerVolatilityFactorServiceTest
{
    private const string BtcUsdt = "BTCUSDT";
    private const string EthUsdt = "ETHUSDT";

    [Fact]
    public void IsFixedUniverse_ShouldReturnFalse()
    {
        // arrange
        var mockBroker = new Mock<IYoloBroker>();
        var service = new BrokerVolatilityFactorService(mockBroker.Object);

        // act
        var result = service.IsFixedUniverse;

        // assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Order_ShouldReturn1000()
    {
        // arrange
        var mockBroker = new Mock<IYoloBroker>();
        var service = new BrokerVolatilityFactorService(mockBroker.Object);

        // act
        var result = service.Order;

        // assert
        result.ShouldBe(1000);
    }

    [Fact]
    public async Task GetFactorsAsync_WhenTickersIsNull_ShouldReturnEmpty()
    {
        // arrange
        var mockBroker = new Mock<IYoloBroker>();
        var service = new BrokerVolatilityFactorService(mockBroker.Object);

        // act
        var result = await service.GetFactorsAsync();

        // assert
        result.ShouldBe(FactorDataFrame.Empty);
        mockBroker.Verify(
            b => b.GetDailyClosePricesAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetFactorsAsync_WhenTickersIsEmpty_ShouldReturnEmpty()
    {
        // arrange
        var mockBroker = new Mock<IYoloBroker>();
        var service = new BrokerVolatilityFactorService(mockBroker.Object);

        // act
        var result = await service.GetFactorsAsync(Array.Empty<string>());

        // assert
        result.ShouldBe(FactorDataFrame.Empty);
        mockBroker.Verify(
            b => b.GetDailyClosePricesAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetFactorsAsync_WhenFactorsContainsVolatility_ShouldReturnEmpty()
    {
        // arrange
        var mockBroker = new Mock<IYoloBroker>();
        var service = new BrokerVolatilityFactorService(mockBroker.Object);
        var factors = new HashSet<FactorType> { FactorType.Volatility };

        // act
        var result = await service.GetFactorsAsync([BtcUsdt], factors);

        // assert
        result.ShouldBe(FactorDataFrame.Empty);
        mockBroker.Verify(
            b => b.GetDailyClosePricesAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetFactorsAsync_WhenValidSingleTicker_ShouldReturnVolatilityFactor()
    {
        // arrange
        var mockBroker = new Mock<IYoloBroker>();
        var prices = new List<decimal> { 100m, 102m, 98m, 101m, 99m, 103m, 97m, 100m, 104m, 96m };

        mockBroker.Setup(b => b.GetDailyClosePricesAsync(
                BtcUsdt,
                30,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);

        var service = new BrokerVolatilityFactorService(mockBroker.Object);

        // act
        var result = await service.GetFactorsAsync([BtcUsdt]);

        // assert
        result.ShouldNotBeNull();
        result.ShouldNotBe(FactorDataFrame.Empty);
        result.Tickers.ShouldBe([BtcUsdt]);
        result.FactorTypes.ShouldBe([FactorType.Volatility]);
        result[FactorType.Volatility, BtcUsdt].ShouldBeGreaterThan(0);

        mockBroker.Verify(
            b => b.GetDailyClosePricesAsync(
                BtcUsdt,
                30,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetFactorsAsync_WhenValidMultipleTickers_ShouldReturnVolatilityForAll()
    {
        // arrange
        var mockBroker = new Mock<IYoloBroker>();
        var btcPrices = new List<decimal> { 100m, 102m, 98m, 101m, 99m, 103m, 97m, 100m, 104m, 96m };
        var ethPrices = new List<decimal> { 50m, 51m, 49m, 50.5m, 49.5m, 51.5m, 48.5m, 50m, 52m, 48m };

        mockBroker.Setup(b => b.GetDailyClosePricesAsync(
                BtcUsdt,
                30,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(btcPrices);
        mockBroker.Setup(b => b.GetDailyClosePricesAsync(
                EthUsdt,
                30,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ethPrices);

        var service = new BrokerVolatilityFactorService(mockBroker.Object);

        // act
        var result = await service.GetFactorsAsync([BtcUsdt, EthUsdt]);

        // assert
        result.ShouldNotBeNull();
        result.ShouldNotBe(FactorDataFrame.Empty);
        result.Tickers.Count.ShouldBe(2);
        result.Tickers.ShouldContain(BtcUsdt);
        result.Tickers.ShouldContain(EthUsdt);
        result.FactorTypes.ShouldBe([FactorType.Volatility]);
        result[FactorType.Volatility, BtcUsdt].ShouldBeGreaterThan(0);
        result[FactorType.Volatility, EthUsdt].ShouldBeGreaterThan(0);

        mockBroker.Verify(
            b => b.GetDailyClosePricesAsync(
                BtcUsdt,
                30,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        mockBroker.Verify(
            b => b.GetDailyClosePricesAsync(
                EthUsdt,
                30,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetFactorsAsync_WhenCancellationRequested_ShouldPassCancellationToken()
    {
        // arrange
        var mockBroker = new Mock<IYoloBroker>();
        var cts = new CancellationTokenSource();
        var prices = new List<decimal> { 100m, 102m, 98m, 101m, 99m, 103m, 97m, 100m, 104m, 96m };

        mockBroker.Setup(b => b.GetDailyClosePricesAsync(
                BtcUsdt,
                30,
                It.IsAny<bool>(),
                cts.Token))
            .ReturnsAsync(prices);

        var service = new BrokerVolatilityFactorService(mockBroker.Object);

        // act
        var result = await service.GetFactorsAsync([BtcUsdt], cancellationToken: cts.Token);

        // assert
        result.ShouldNotBeNull();
        mockBroker.Verify(
            b => b.GetDailyClosePricesAsync(
                BtcUsdt,
                30,
                It.IsAny<bool>(),
                cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task GetFactorsAsync_WhenFactorsFilterDoesNotContainVolatility_ShouldCallBroker()
    {
        // arrange
        var mockBroker = new Mock<IYoloBroker>();
        var prices = new List<decimal> { 100m, 102m, 98m, 101m, 99m, 103m, 97m, 100m, 104m, 96m };
        var factors = new HashSet<FactorType> { FactorType.Carry, FactorType.Momentum };

        mockBroker.Setup(b => b.GetDailyClosePricesAsync(
                BtcUsdt,
                30,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);

        var service = new BrokerVolatilityFactorService(mockBroker.Object);

        // act
        var result = await service.GetFactorsAsync([BtcUsdt], factors);

        // assert
        result.ShouldNotBeNull();
        result.ShouldNotBe(FactorDataFrame.Empty);
        mockBroker.Verify(
            b => b.GetDailyClosePricesAsync(
                BtcUsdt,
                30,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetFactorsAsync_WhenNullFactors_ShouldCallBroker()
    {
        // arrange
        var mockBroker = new Mock<IYoloBroker>();
        var prices = new List<decimal> { 100m, 102m, 98m, 101m, 99m, 103m, 97m, 100m, 104m, 96m };

        mockBroker.Setup(b => b.GetDailyClosePricesAsync(
                BtcUsdt,
                30,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);

        var service = new BrokerVolatilityFactorService(mockBroker.Object);

        // act
        var result = await service.GetFactorsAsync([BtcUsdt]);

        // assert
        result.ShouldNotBeNull();
        result.ShouldNotBe(FactorDataFrame.Empty);
        mockBroker.Verify(
            b => b.GetDailyClosePricesAsync(
                BtcUsdt,
                30,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}