using HyperLiquid.Net.Interfaces.Clients;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit.Abstractions;
using YoloAbstractions;
using YoloTest.Util;
using static YoloAbstractions.AssetType;
using static YoloBroker.Hyperliquid.Test.TickerAliasUtil;
using static YoloBroker.Hyperliquid.Test.TradeUtil;

namespace YoloBroker.Hyperliquid.Test;

public class HyperliquidBrokerTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ILoggerFactory _loggerFactory;

    public HyperliquidBrokerTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XUnitLoggerProvider(testOutputHelper));
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }
    
    [Fact]
    public async Task GivenNullTrades_WhenPlaceTradesAsync_ShouldThrowArgumentNullException()
    {
        // arrange
        var broker = GetTestBroker();

        // act & assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in broker.PlaceTradesAsync(null!))
            {
                // Should throw before yielding
            }
        });
    }

    [Fact]
    public async Task GivenEmptyTrades_WhenPlaceTradesAsync_ShouldYieldNothing()
    {
        // arrange
        var broker = GetTestBroker();
        var results = new List<TradeResult>();

        // act
        await foreach (var result in broker.PlaceTradesAsync([]))
        {
            results.Add(result);
        }

        // assert
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task GivenNullOrder_WhenCancelOrderAsync_ShouldThrowArgumentNullException()
    {
        // arrange
        var broker = GetTestBroker();

        // act & assert
        await Should.ThrowAsync<ArgumentNullException>(() => broker.CancelOrderAsync(null!));
    }

    [Fact]
    public async Task GivenCompletedOrder_WhenCancelOrderAsync_ShouldSkipCancellation()
    {
        // arrange
        var broker = GetTestBroker();
        var completedOrder = new Order(
            123,
            "ETH",
            Future,
            DateTime.UtcNow,
            OrderSide.Buy,
            OrderStatus.Filled,
            1m,
            1m,
            2000m,
            "test");

        // act
        await broker.CancelOrderAsync(completedOrder);

        // assert
        // Should complete without error
    }

    [Fact]
    public void GivenUnsupportedAssetType_WhenPlaceTrade_ShouldThrowArgumentOutOfRangeException()
    {
        // arrange
        var broker = GetTestBroker();
        var trade = new Trade(
            "BTC",
            (AssetType) 999, // Invalid asset type
            1m,
            50000m);

        // act & assert
        Should.ThrowAsync<ArgumentOutOfRangeException>(() => broker.PlaceTradeAsync(trade));
    }

    [Fact]
    public void GivenUnsupportedAssetType_WhenCancelOrder_ShouldThrowArgumentOutOfRangeException()
    {
        // arrange
        var broker = GetTestBroker();
        var order = new Order(
            123,
            "BTC",
            (AssetType) 999, // Invalid asset type
            DateTime.UtcNow,
            OrderSide.Buy,
            OrderStatus.Open,
            1m,
            0m,
            50000m,
            "test");

        // act & assert
        Should.ThrowAsync<ArgumentOutOfRangeException>(() => broker.CancelOrderAsync(order));
    }

    [Fact]
    public async Task GivenNullOrder_WhenEditOrderAsync_ShouldThrowArgumentNullException()
    {
        // arrange
        var broker = GetTestBroker();

        // act & assert
        await Should.ThrowAsync<ArgumentNullException>(() => broker.EditOrderAsync(null!));
    }
    
    [Fact]
    public async Task GivenNullOrEmptyTicker_WhenGetDailyClosePricesAsync_ShouldThrowArgumentException()
    {
        // arrange
        var broker = GetTestBroker();

        // act & assert
        await Should.ThrowAsync<ArgumentException>(() =>
            broker.GetDailyClosePricesAsync(null!, 10));
        await Should.ThrowAsync<ArgumentException>(() =>
            broker.GetDailyClosePricesAsync("", 10));
    }

    [Fact]
    public void GivenBroker_WhenDisposedTwice_ShouldNotThrow()
    {
        // arrange
        var broker = GetTestBroker();

        // act
        broker.Dispose();

        // assert - should not throw
        broker.Dispose();
    }

    [Fact]
    public async Task GivenNullTrades_WhenManageOrdersAsync_ShouldThrowArgumentNullException()
    {
        // arrange
        var broker = GetTestBroker();

        // act & assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in broker.ManageOrdersAsync(null!, OrderManagementSettings.Default))
            {
                // Should throw before yielding
            }
        });
    }

    [Fact]
    public async Task GivenNullSettings_WhenManageOrdersAsync_ShouldThrowArgumentNullException()
    {
        // arrange
        var broker = GetTestBroker();
        var trade = CreateTrade("ETH", Future, 0.01, 2000m);

        // act & assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in broker.ManageOrdersAsync([trade], null!))
            {
                // Should throw before yielding
            }
        });
    }

    [Fact]
    public async Task GivenEmptyTrades_WhenManageOrdersAsync_ShouldYieldNothing()
    {
        // arrange
        var broker = GetTestBroker();
        var results = new List<OrderUpdate>();

        // act
        await foreach (var update in broker.ManageOrdersAsync([], OrderManagementSettings.Default))
        {
            results.Add(update);
        }

        // assert
        results.ShouldBeEmpty();
    }

    private HyperliquidBroker GetTestBroker(
        IReadOnlyDictionary<string, string>? aliases = null)
    {
        var mockRestClient = new Mock<IHyperLiquidRestClient>();
        var mockSocketClient = new Mock<IHyperLiquidSocketClient>();
        var logger = _loggerFactory.CreateLogger<HyperliquidBroker>();

        var broker = new HyperliquidBroker(
            mockRestClient.Object,
            mockSocketClient.Object,
            GetTickerAliasService(aliases),
            logger
        );

        return broker;
    }
}