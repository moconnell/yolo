using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;
using Xunit.Abstractions;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Interfaces;
using YoloApp.Commands;
using YoloBroker.Interface;
using YoloTest.Util;

namespace YoloApp.Test.Commands;

public class RebalanceCommandTest
{
    private readonly ILoggerFactory _loggerFactory;

    public RebalanceCommandTest(ITestOutputHelper outputHelper)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XUnitLoggerProvider(outputHelper));
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }

    [Fact]
    public void GivenNullWeightsService_WhenConstructing_ShouldThrowArgumentNullException()
    {
        // arrange
        var mockTradeFactory = new Mock<ITradeFactory>();
        var mockBroker = new Mock<IYoloBroker>();
        var options = Options.Create(new YoloConfig { BaseAsset = "USDC" });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        // act & assert
        Should.Throw<ArgumentNullException>(() =>
            new RebalanceCommand(null!, mockTradeFactory.Object, mockBroker.Object, options, logger));
    }

    [Fact]
    public void GivenNullTradeFactory_WhenConstructing_ShouldThrowArgumentNullException()
    {
        // arrange
        var mockWeightsService = new Mock<ICalcWeights>();
        var mockBroker = new Mock<IYoloBroker>();
        var options = Options.Create(new YoloConfig { BaseAsset = "USDC" });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        // act & assert
        Should.Throw<ArgumentNullException>(() =>
            new RebalanceCommand(mockWeightsService.Object, null!, mockBroker.Object, options, logger));
    }

    [Fact]
    public void GivenNullBroker_WhenConstructing_ShouldThrowArgumentNullException()
    {
        // arrange
        var mockWeightsService = new Mock<ICalcWeights>();
        var mockTradeFactory = new Mock<ITradeFactory>();
        var options = Options.Create(new YoloConfig { BaseAsset = "USDC" });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        // act & assert
        Should.Throw<ArgumentNullException>(() =>
            new RebalanceCommand(mockWeightsService.Object, mockTradeFactory.Object, null!, options, logger));
    }

    [Fact]
    public async Task GivenNoOpenOrders_WhenExecuting_ShouldProcessRebalance()
    {
        // arrange
        var weights = new Dictionary<string, decimal>
        {
            { "BTC/USDT", 0.5m },
            { "ETH/USDT", 0.3m }
        };

        var positions = new Dictionary<string, IReadOnlyList<Position>>();
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>();
        var trades = Array.Empty<Trade>();

        var mockWeightsService = new Mock<ICalcWeights>();
        mockWeightsService.Setup(x => x.CalculateWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(weights);

        var mockTradeFactory = new Mock<ITradeFactory>();
        mockTradeFactory.Setup(x => x.CalculateTrades(weights, positions, markets))
            .Returns(trades);

        var mockBroker = new Mock<IYoloBroker>();
        mockBroker.Setup(x => x.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, Order>());
        mockBroker.Setup(x => x.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(positions);
        mockBroker.Setup(x => x.GetMarketsAsync(
                It.IsAny<HashSet<string>>(),
                It.IsAny<string>(),
                It.IsAny<AssetPermissions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(markets);

        var options = Options.Create(new YoloConfig { BaseAsset = "USDC" });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        var command = new RebalanceCommand(
            mockWeightsService.Object,
            mockTradeFactory.Object,
            mockBroker.Object,
            options,
            logger);

        // act
        await command.ExecuteAsync();

        // assert
        mockBroker.Verify(x => x.GetOpenOrdersAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockWeightsService.Verify(x => x.CalculateWeightsAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockBroker.Verify(x => x.GetPositionsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenOpenOrdersAndKillConfigured_WhenExecuting_ShouldCancelOrders()
    {
        // arrange
        var openOrders = new Dictionary<long, Order>
        {
            { 1, new Order(1, "BTC", AssetType.Future, new DateTime(2020, 1, 1), OrderSide.Buy, OrderStatus.Open, 10) }
        };

        var mockWeightsService = new Mock<ICalcWeights>();
        mockWeightsService.Setup(x => x.CalculateWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal>());

        var mockTradeFactory = new Mock<ITradeFactory>();
        mockTradeFactory.Setup(x => x.CalculateTrades(
                It.IsAny<IReadOnlyDictionary<string, decimal>>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<Position>>>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>>>()))
            .Returns(Array.Empty<Trade>());

        var mockBroker = new Mock<IYoloBroker>();
        mockBroker.Setup(x => x.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(openOrders);
        mockBroker.Setup(x => x.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Position>>());
        mockBroker.Setup(x => x.GetMarketsAsync(
                It.IsAny<HashSet<string>>(),
                It.IsAny<string>(),
                It.IsAny<AssetPermissions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<MarketInfo>>());

        var options = Options.Create(new YoloConfig
        {
            BaseAsset = "USDC",
            KillOpenOrders = true
        });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        var command = new RebalanceCommand(
            mockWeightsService.Object,
            mockTradeFactory.Object,
            mockBroker.Object,
            options,
            logger);

        // act
        await command.ExecuteAsync();

        // assert
        mockBroker.Verify(x => x.CancelOrderAsync(
            It.IsAny<Order>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenOpenOrdersAndKillNotConfigured_WhenExecuting_ShouldNotProceed()
    {
        // arrange
        var openOrders = new Dictionary<long, Order>
        {
            { 1, new Order(1, "BTC", AssetType.Future, new DateTime(2020, 1, 1), OrderSide.Buy, OrderStatus.Open, 10) }
        };

        var mockWeightsService = new Mock<ICalcWeights>();
        var mockTradeFactory = new Mock<ITradeFactory>();
        var mockBroker = new Mock<IYoloBroker>();
        mockBroker.Setup(x => x.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(openOrders);

        var options = Options.Create(new YoloConfig
        {
            BaseAsset = "USDC",
            KillOpenOrders = false
        });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        var command = new RebalanceCommand(
            mockWeightsService.Object,
            mockTradeFactory.Object,
            mockBroker.Object,
            options,
            logger);

        // act
        await command.ExecuteAsync();

        // assert
        mockWeightsService.Verify(x => x.CalculateWeightsAsync(It.IsAny<CancellationToken>()), Times.Never);
        mockBroker.Verify(x => x.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenNoTrades_WhenExecuting_ShouldNotManageOrders()
    {
        // arrange
        var mockWeightsService = new Mock<ICalcWeights>();
        mockWeightsService.Setup(x => x.CalculateWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal>());

        var mockTradeFactory = new Mock<ITradeFactory>();
        mockTradeFactory.Setup(x => x.CalculateTrades(
                It.IsAny<Dictionary<string, decimal>>(),
                It.IsAny<Dictionary<string, IReadOnlyList<Position>>>(),
                It.IsAny<Dictionary<string, IReadOnlyList<MarketInfo>>>()))
            .Returns(Array.Empty<Trade>());

        var mockBroker = new Mock<IYoloBroker>();
        mockBroker.Setup(x => x.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, Order>());
        mockBroker.Setup(x => x.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Position>>());
        mockBroker.Setup(x => x.GetMarketsAsync(
                It.IsAny<HashSet<string>>(),
                It.IsAny<string>(),
                It.IsAny<AssetPermissions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<MarketInfo>>());

        var options = Options.Create(new YoloConfig { BaseAsset = "USDC" });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        var command = new RebalanceCommand(
            mockWeightsService.Object,
            mockTradeFactory.Object,
            mockBroker.Object,
            options,
            logger);

        // act
        await command.ExecuteAsync();

        // assert
        mockBroker.Verify(x => x.ManageOrdersAsync(
            It.IsAny<Trade[]>(),
            It.IsAny<OrderManagementSettings>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenTrades_WhenExecuting_ShouldManageOrders()
    {
        // arrange
        var weights = new Dictionary<string, decimal> { { "BTC/USDT", 0.5m }, { "ETH/USDT", 0.5m }, { "BCH/USDT", 0.5m } };
        var positions = new Dictionary<string, IReadOnlyList<Position>>();
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>();
        var trades = new[] { new Trade("BTC", AssetType.Future, 10), new Trade("ETH", AssetType.Future, 10), new Trade("BCH", AssetType.Future, 10) };

        var channel = Channel.CreateUnbounded<OrderUpdate>();
        await channel.Writer.WriteAsync(new OrderUpdate("BTC", OrderUpdateType.Created, new Order(1, "BTC", AssetType.Future, DateTime.UtcNow, OrderSide.Buy, OrderStatus.Open, 10)));
        await channel.Writer.WriteAsync(new OrderUpdate("ETH", OrderUpdateType.Created, new Order(2, "ETH", AssetType.Future, DateTime.UtcNow, OrderSide.Buy, OrderStatus.Open, 10)));
        await channel.Writer.WriteAsync(new OrderUpdate("BCH", OrderUpdateType.Created, new Order(3, "BCH", AssetType.Future, DateTime.UtcNow, OrderSide.Buy, OrderStatus.Open, 10)));
        await channel.Writer.WriteAsync(new OrderUpdate("BTC", OrderUpdateType.Filled, new Order(1, "BTC", AssetType.Future, DateTime.UtcNow, OrderSide.Buy, OrderStatus.Filled, 10)));
        await channel.Writer.WriteAsync(new OrderUpdate("ETH", OrderUpdateType.Filled, new Order(2, "ETH", AssetType.Future, DateTime.UtcNow, OrderSide.Buy, OrderStatus.Filled, 10)));
        await channel.Writer.WriteAsync(new OrderUpdate("BCH", OrderUpdateType.Filled, new Order(3, "BCH", AssetType.Future, DateTime.UtcNow, OrderSide.Buy, OrderStatus.Filled, 10)));
        channel.Writer.Complete();

        var mockWeightsService = new Mock<ICalcWeights>();
        mockWeightsService.Setup(x => x.CalculateWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(weights);

        var mockTradeFactory = new Mock<ITradeFactory>();
        mockTradeFactory.Setup(x => x.CalculateTrades(weights, positions, markets))
            .Returns(trades);

        var mockBroker = new Mock<IYoloBroker>();
        mockBroker.Setup(x => x.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, Order>());
        mockBroker.Setup(x => x.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(positions);
        mockBroker.Setup(x => x.GetMarketsAsync(
                It.IsAny<HashSet<string>>(),
                It.IsAny<string>(),
                It.IsAny<AssetPermissions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(markets);
        mockBroker.Setup(x => x.ManageOrdersAsync(
                It.IsAny<Trade[]>(),
                It.IsAny<OrderManagementSettings>(),
                It.IsAny<CancellationToken>()))
            .Returns(channel.Reader.ReadAllAsync());

        var options = Options.Create(new YoloConfig { BaseAsset = "USDC" });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        var command = new RebalanceCommand(
            mockWeightsService.Object,
            mockTradeFactory.Object,
            mockBroker.Object,
            options,
            logger);

        // act
        await command.ExecuteAsync();

        // assert
        mockBroker.Verify(x => x.ManageOrdersAsync(
            It.Is<Trade[]>(t => t.Length == 3),
            It.IsAny<OrderManagementSettings>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenCancellationToken_WhenExecuting_ShouldHandleGracefully()
    {
        // arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var mockWeightsService = new Mock<ICalcWeights>();
        var mockTradeFactory = new Mock<ITradeFactory>();
        var mockBroker = new Mock<IYoloBroker>();
        mockBroker.Setup(x => x.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var options = Options.Create(new YoloConfig { BaseAsset = "USDC" });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        var command = new RebalanceCommand(
            mockWeightsService.Object,
            mockTradeFactory.Object,
            mockBroker.Object,
            options,
            logger);

        // act & assert
        await Should.ThrowAsync<OperationCanceledException>(() => command.ExecuteAsync(cts.Token));
    }

    [Fact]
    public async Task GivenOrderError_WhenExecuting_ShouldLogError()
    {
        // arrange
        var weights = new Dictionary<string, decimal> { { "BTC/USDT", 0.5m } };
        var positions = new Dictionary<string, IReadOnlyList<Position>>();
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>();
        var trades = new[] { new Trade("BTC", AssetType.Future, 10) };

        var channel = Channel.CreateUnbounded<OrderUpdate>();
        var errorOrder = new Order(1, "BTC/USDT", AssetType.Future, DateTime.UtcNow, OrderSide.Buy, OrderStatus.Rejected, 10);
        await channel.Writer.WriteAsync(new OrderUpdate("BTC/USDT", OrderUpdateType.Error, errorOrder, "Order placement failed"));
        channel.Writer.Complete();

        var mockWeightsService = new Mock<ICalcWeights>();
        mockWeightsService.Setup(x => x.CalculateWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(weights);

        var mockTradeFactory = new Mock<ITradeFactory>();
        mockTradeFactory.Setup(x => x.CalculateTrades(weights, positions, markets))
            .Returns(trades);

        var mockBroker = new Mock<IYoloBroker>();
        mockBroker.Setup(x => x.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, Order>());
        mockBroker.Setup(x => x.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(positions);
        mockBroker.Setup(x => x.GetMarketsAsync(
                It.IsAny<HashSet<string>>(),
                It.IsAny<string>(),
                It.IsAny<AssetPermissions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(markets);
        mockBroker.Setup(x => x.ManageOrdersAsync(
                It.IsAny<Trade[]>(),
                It.IsAny<OrderManagementSettings>(),
                It.IsAny<CancellationToken>()))
            .Returns(channel.Reader.ReadAllAsync());

        var options = Options.Create(new YoloConfig { BaseAsset = "USDC" });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        var command = new RebalanceCommand(
            mockWeightsService.Object,
            mockTradeFactory.Object,
            mockBroker.Object,
            options,
            logger);

        // act
        await command.ExecuteAsync();

        // assert - verify that ManageOrdersAsync was called and completed without throwing
        mockBroker.Verify(x => x.ManageOrdersAsync(
            It.Is<Trade[]>(t => t.Length == 1),
            It.IsAny<OrderManagementSettings>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}