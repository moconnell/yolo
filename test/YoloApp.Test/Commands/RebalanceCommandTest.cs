using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Sockets;
using System.Text.Json;
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
        var mockOrderManager = new Mock<IOrderManager>();
        var options = Options.Create(new YoloConfig { BaseAsset = "USDC" });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        // act & assert
        Should.Throw<ArgumentNullException>(() =>
            new RebalanceCommand(null!, mockTradeFactory.Object, mockOrderManager.Object, mockBroker.Object, options, logger));
    }

    [Fact]
    public void GivenNullTradeFactory_WhenConstructing_ShouldThrowArgumentNullException()
    {
        // arrange
        var mockWeightsService = new Mock<ICalcWeights>();
        var mockBroker = new Mock<IYoloBroker>();
        var mockOrderManager = new Mock<IOrderManager>();
        var options = Options.Create(new YoloConfig { BaseAsset = "USDC" });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        // act & assert
        Should.Throw<ArgumentNullException>(() =>
            new RebalanceCommand(mockWeightsService.Object, null!, mockOrderManager.Object, mockBroker.Object, options, logger));
    }

    [Fact]
    public void GivenNullBroker_WhenConstructing_ShouldThrowArgumentNullException()
    {
        // arrange
        var mockWeightsService = new Mock<ICalcWeights>();
        var mockTradeFactory = new Mock<ITradeFactory>();
        var mockOrderManager = new Mock<IOrderManager>();
        var mockBroker = new Mock<IYoloBroker>();
        var options = Options.Create(new YoloConfig { BaseAsset = "USDC" });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        // act & assert
        Should.Throw<ArgumentNullException>(() =>
            new RebalanceCommand(mockWeightsService.Object, mockTradeFactory.Object, mockOrderManager.Object, null!, options, logger));
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
            .ReturnsAsync(WeightsCalculationResult.FromWeights(weights));

        var mockTradeFactory = new Mock<ITradeFactory>();
        mockTradeFactory.Setup(x => x.CalculateTrades(weights, positions, markets))
            .Returns(trades);

        var mockOrderManager = new Mock<IOrderManager>();

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
        mockBroker.Setup(x => x.GetAccountContext())
            .Returns(new BrokerAccountContext("0xwallet", "0xvault", true));

        var options = Options.Create(new YoloConfig { BaseAsset = "USDC" });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        var command = new RebalanceCommand(
            mockWeightsService.Object,
            mockTradeFactory.Object,
            mockOrderManager.Object,
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
            .ReturnsAsync(WeightsCalculationResult.FromWeights(new Dictionary<string, decimal>()));

        var mockTradeFactory = new Mock<ITradeFactory>();
        mockTradeFactory.Setup(x => x.CalculateTrades(
                It.IsAny<IReadOnlyDictionary<string, decimal>>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<Position>>>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>>>()))
            .Returns(Array.Empty<Trade>());

        var mockOrderManager = new Mock<IOrderManager>();

        var mockBroker = new Mock<IYoloBroker>();
        mockBroker.Setup(x => x.GetAccountContext())
            .Returns(new BrokerAccountContext("0xwallet", "0xvault", true));
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
            mockOrderManager.Object,
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
        var mockOrderManager = new Mock<IOrderManager>();
        var mockBroker = new Mock<IYoloBroker>();
        mockBroker.Setup(x => x.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(openOrders);
        mockBroker.Setup(x => x.GetAccountContext())
            .Returns(new BrokerAccountContext("0xwallet", "0xvault", true));

        var options = Options.Create(new YoloConfig
        {
            BaseAsset = "USDC",
            KillOpenOrders = false
        });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        var command = new RebalanceCommand(
            mockWeightsService.Object,
            mockTradeFactory.Object,
            mockOrderManager.Object,
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
            .ReturnsAsync(WeightsCalculationResult.FromWeights(new Dictionary<string, decimal>()));

        var mockTradeFactory = new Mock<ITradeFactory>();
        mockTradeFactory.Setup(x => x.CalculateTrades(
                It.IsAny<Dictionary<string, decimal>>(),
                It.IsAny<Dictionary<string, IReadOnlyList<Position>>>(),
                It.IsAny<Dictionary<string, IReadOnlyList<MarketInfo>>>()))
            .Returns(Array.Empty<Trade>());

        var mockOrderManager = new Mock<IOrderManager>();
        var mockBroker = new Mock<IYoloBroker>();
        mockBroker.Setup(x => x.GetAccountContext())
            .Returns(new BrokerAccountContext("0xwallet", "0xvault", true));
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
            mockOrderManager.Object,
            mockBroker.Object,
            options,
            logger);

        // act
        await command.ExecuteAsync();

        // assert
        mockOrderManager.Verify(x => x.ManageOrdersAsync(
            It.IsAny<Trade[]>(),
            It.IsAny<OrderManagementSettings>(),
            It.IsAny<ITradeAdvisor>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenMissingBidAskAndNoGeneratedTrades_WhenExecuting_ShouldCompleteWithoutError()
    {
        // arrange
        var weights = new Dictionary<string, decimal>
        {
            { "HYPE/USDC", 0.2m }
        };

        var positions = new Dictionary<string, IReadOnlyList<Position>>
        {
            {
                "HYPE",
                [
                    new Position("HYPE-Future", "HYPE", AssetType.Future, 1.0m)
                ]
            }
        };

        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>
        {
            {
                "HYPE",
                [
                    new MarketInfo(
                        "HYPE-Future",
                        "HYPE",
                        "USDC",
                        AssetType.Future,
                        DateTime.UtcNow,
                        PriceStep: 0.01m,
                        QuantityStep: 0.01m,
                        MinProvideSize: 0.07m,
                        Ask: null,
                        Bid: null,
                        Last: 163.05m)
                ]
            }
        };

        var mockWeightsService = new Mock<ICalcWeights>();
        mockWeightsService.Setup(x => x.CalculateWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(WeightsCalculationResult.FromWeights(weights));

        var mockTradeFactory = new Mock<ITradeFactory>();
        mockTradeFactory.Setup(x => x.CalculateTrades(weights, positions, markets))
            .Returns(Array.Empty<Trade>());

        var mockOrderManager = new Mock<IOrderManager>();

        var mockBroker = new Mock<IYoloBroker>();
        mockBroker.Setup(x => x.GetAccountContext())
            .Returns(new BrokerAccountContext("0xwallet", "0xvault", true));
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
            mockOrderManager.Object,
            mockBroker.Object,
            options,
            logger);

        // act
        await command.ExecuteAsync();

        // assert
        mockTradeFactory.Verify(x => x.CalculateTrades(weights, positions, markets), Times.Once);
        mockOrderManager.Verify(x => x.ManageOrdersAsync(
            It.IsAny<Trade[]>(),
            It.IsAny<OrderManagementSettings>(),
            It.IsAny<ITradeAdvisor>(),
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
            .ReturnsAsync(WeightsCalculationResult.FromWeights(weights));

        var mockTradeFactory = new Mock<ITradeFactory>();
        mockTradeFactory.Setup(x => x.CalculateTrades(weights, positions, markets))
            .Returns(trades);

        var mockBroker = new Mock<IYoloBroker>();
        mockBroker.Setup(x => x.GetAccountContext())
            .Returns(new BrokerAccountContext("0xwallet", "0xvault", true));
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

        var mockOrderManager = new Mock<IOrderManager>();
        mockOrderManager.Setup(x => x.ManageOrdersAsync(
                It.IsAny<Trade[]>(),
                It.IsAny<OrderManagementSettings>(),
            It.IsAny<ITradeAdvisor>(),
            It.IsAny<CancellationToken>()))
            .Returns(channel.Reader.ReadAllAsync());

        var options = Options.Create(new YoloConfig { BaseAsset = "USDC" });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();

        var command = new RebalanceCommand(
            mockWeightsService.Object,
            mockTradeFactory.Object,
            mockOrderManager.Object,
            mockBroker.Object,
            options,
            logger);

        // act
        await command.ExecuteAsync();

        // assert
        mockOrderManager.Verify(x => x.ManageOrdersAsync(
            It.Is<Trade[]>(t => t.Length == 3),
            It.IsAny<OrderManagementSettings>(),
            It.IsAny<ITradeAdvisor>(),
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
        var mockOrderManager = new Mock<IOrderManager>();
        var mockBroker = new Mock<IYoloBroker>();
        mockBroker.Setup(x => x.GetAccountContext())
            .Returns(new BrokerAccountContext("0xwallet", "0xvault", true));
        mockBroker.Setup(x => x.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var options = Options.Create(new YoloConfig { BaseAsset = "USDC" });
        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();
        var command = new RebalanceCommand(
            mockWeightsService.Object,
            mockTradeFactory.Object,
            mockOrderManager.Object,
            mockBroker.Object,
            options,
            logger);

        // act & assert
        await Should.ThrowAsync<OperationCanceledException>(() => command.ExecuteAsync(cts.Token));
    }

    [Fact]
    public async Task GivenError_WhenExecuting_ShouldLogError()
    {
        // arrange
        const string errorMessage = "Unexpected error during order management";

        var cts = new CancellationTokenSource();
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
            .ReturnsAsync(WeightsCalculationResult.FromWeights(weights));

        var mockTradeFactory = new Mock<ITradeFactory>();
        mockTradeFactory.Setup(x => x.CalculateTrades(weights, positions, markets))
            .Returns(trades);

        var mockOrderManager = new Mock<IOrderManager>();
        mockOrderManager.Setup(x => x.ManageOrdersAsync(
                It.IsAny<Trade[]>(),
                It.IsAny<OrderManagementSettings>(),
            It.IsAny<ITradeAdvisor>(),
            It.IsAny<CancellationToken>()))
            .Throws<SocketException>();

        var mockBroker = new Mock<IYoloBroker>();
        mockBroker.Setup(x => x.GetAccountContext())
            .Returns(new BrokerAccountContext("0xwallet", "0xvault", true));
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
        Mock<ILogger<RebalanceCommand>> mock = new();
        var logger = mock.Object;

        var command = new RebalanceCommand(
            mockWeightsService.Object,
            mockTradeFactory.Object,
            mockOrderManager.Object,
            mockBroker.Object,
            options,
            logger);

        // act & assert
        await command.ExecuteAsync(cts.Token);

        // assert - verify that ManageOrdersAsync was called and completed without throwing
        mock.Verify(x => x.Log(
            It.Is<LogLevel>(l => l == LogLevel.Error),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(errorMessage)),
            It.IsAny<SocketException>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)), Times.Once);
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
            .ReturnsAsync(WeightsCalculationResult.FromWeights(weights));

        var mockTradeFactory = new Mock<ITradeFactory>();
        mockTradeFactory.Setup(x => x.CalculateTrades(weights, positions, markets))
            .Returns(trades);

        var mockOrderManager = new Mock<IOrderManager>();
        mockOrderManager.Setup(x => x.ManageOrdersAsync(
                It.IsAny<Trade[]>(),
                It.IsAny<OrderManagementSettings>(),
            It.IsAny<ITradeAdvisor>(),
            It.IsAny<CancellationToken>()))
            .Returns(channel.Reader.ReadAllAsync());

        var mockBroker = new Mock<IYoloBroker>();
        mockBroker.Setup(x => x.GetAccountContext())
            .Returns(new BrokerAccountContext("0xwallet", "0xvault", true));
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
            mockOrderManager.Object,
            mockBroker.Object,
            options,
            logger);

        // act
        await command.ExecuteAsync();

        // assert - verify that ManageOrdersAsync was called and completed without throwing
        mockOrderManager.Verify(x => x.ManageOrdersAsync(
            It.Is<Trade[]>(t => t.Length == 1),
            It.IsAny<OrderManagementSettings>(),
            It.IsAny<ITradeAdvisor>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenOrderUpdates_WhenExecuting_ShouldRecordEventTelemetry()
    {
        // arrange
        var weights = new Dictionary<string, decimal> { { "BTC/USDT", 0.5m } };
        var positions = new Dictionary<string, IReadOnlyList<Position>>
        {
            ["BTC"] = [new Position("BTC", "BTC", AssetType.Future, 1.5m)]
        };
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>
        {
            ["BTC"] =
            [
                new MarketInfo(
                    "BTC",
                    "BTC",
                    "USDC",
                    AssetType.Future,
                    DateTime.UtcNow,
                    Ask: 101m,
                    Bid: 99m,
                    Mid: 100m)
            ]
        };
        var trade = new Trade("BTC", AssetType.Future, 2m, 100.5m, ClientOrderId: "client-1");
        var created = new Order(123, "BTC", AssetType.Future, DateTime.UtcNow, OrderSide.Buy, OrderStatus.Open, 2m, 0.5m, 100.5m, "client-1");
        var filled = created with { OrderStatus = OrderStatus.Filled, Filled = 2m };

        var channel = Channel.CreateUnbounded<OrderUpdate>();
        await channel.Writer.WriteAsync(new OrderUpdate("BTC", OrderUpdateType.Created, created, "accepted"));
        await channel.Writer.WriteAsync(new OrderUpdate("BTC", OrderUpdateType.Filled, filled));
        channel.Writer.Complete();

        var mockWeightsService = new Mock<ICalcWeights>();
        mockWeightsService.Setup(x => x.CalculateWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(WeightsCalculationResult.FromWeights(weights));

        var mockTradeFactory = new Mock<ITradeFactory>();
        mockTradeFactory.Setup(x => x.CalculateTrades(weights, positions, markets))
            .Returns([trade]);

        var mockOrderManager = new Mock<IOrderManager>();
        mockOrderManager.Setup(x => x.ManageOrdersAsync(
                It.IsAny<Trade[]>(),
                It.IsAny<OrderManagementSettings>(),
                It.IsAny<ITradeAdvisor>(),
                It.IsAny<CancellationToken>()))
            .Returns(channel.Reader.ReadAllAsync());

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
        mockBroker.Setup(x => x.GetAccountContext())
            .Returns(new BrokerAccountContext("0xwallet", "0xvault", true));

        var rebalanceEvents = new List<RebalanceEventRecord>();
        var mockEventRecorder = new Mock<IRebalanceEventRecorder>();
        mockEventRecorder.Setup(x => x.RecordAsync(It.IsAny<RebalanceEventRecord>(), It.IsAny<CancellationToken>()))
            .Callback<RebalanceEventRecord, CancellationToken>((record, _) => rebalanceEvents.Add(record))
            .Returns(Task.CompletedTask);

        var logger = _loggerFactory.CreateLogger<RebalanceCommand>();
        var command = new RebalanceCommand(
            mockWeightsService.Object,
            mockTradeFactory.Object,
            mockOrderManager.Object,
            mockBroker.Object,
            new YoloConfig { BaseAsset = "USDC" },
            logger,
            mockEventRecorder.Object,
            "yolodaily");

        // act
        await command.ExecuteAsync();

        // assert
        rebalanceEvents.ShouldContain(e => e.EventType == "RunStarted");
        rebalanceEvents.ShouldContain(e => e.EventType == "PositionsFetched");
        rebalanceEvents.ShouldContain(e => e.EventType == "WeightsCalculated");
        rebalanceEvents.ShouldContain(e => e.EventType == "MarketsFetched");
        rebalanceEvents.ShouldContain(e => e.EventType == "RebalancePlanCalculated");
        rebalanceEvents.ShouldContain(e => e.EventType == "TradeProposed" && e.ClientOrderId == "client-1" && e.Coin == "BTC");
        rebalanceEvents.ShouldContain(e => e.EventType == "OrderUpdate" && e.OrderId == "123");
        rebalanceEvents.ShouldContain(e => e.EventType == "RunCompleted");
        rebalanceEvents.Select(e => e.Sequence).ShouldBe(Enumerable.Range(1, rebalanceEvents.Count));
    }

    [Fact]
    public async Task GivenWeightsCalculationResult_WhenExecuting_ShouldRecordFactorSnapshots()
    {
        // arrange
        var weights = new Dictionary<string, decimal>
        {
            ["BTC/USDT"] = 0.5m,
            ["ETH/USDT"] = -0.25m
        };
        var rawFactors = FactorDataFrame.NewFrom(
            ["BTC/USDT", "ETH/USDT"],
            DateTime.Today,
            (FactorType.Carry, [0.1d, -0.2d]),
            (FactorType.Volatility, [0.42d, double.NaN]));
        var normalizedFactors = FactorDataFrame.NewFrom(
            ["BTC/USDT", "ETH/USDT"],
            DateTime.Today,
            (FactorType.Carry, [1d, -1d]),
            (FactorType.Volatility, [0.42d, double.NaN]));
        var positions = new Dictionary<string, IReadOnlyList<Position>>();
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>();

        var mockWeightsService = new Mock<ICalcWeights>();
        mockWeightsService.Setup(x => x.CalculateWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeightsCalculationResult(weights, rawFactors, normalizedFactors));

        var mockTradeFactory = new Mock<ITradeFactory>();
        mockTradeFactory.Setup(x => x.CalculateTrades(weights, positions, markets))
            .Returns([]);

        var mockOrderManager = new Mock<IOrderManager>();

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
        mockBroker.Setup(x => x.GetAccountContext())
            .Returns(new BrokerAccountContext("0xwallet", "0xvault", true));

        var rebalanceEvents = new List<RebalanceEventRecord>();
        var mockEventRecorder = new Mock<IRebalanceEventRecorder>();
        mockEventRecorder.Setup(x => x.RecordAsync(It.IsAny<RebalanceEventRecord>(), It.IsAny<CancellationToken>()))
            .Callback<RebalanceEventRecord, CancellationToken>((record, _) => rebalanceEvents.Add(record))
            .Returns(Task.CompletedTask);

        var command = new RebalanceCommand(
            mockWeightsService.Object,
            mockTradeFactory.Object,
            mockOrderManager.Object,
            mockBroker.Object,
            new YoloConfig { BaseAsset = "USDC" },
            _loggerFactory.CreateLogger<RebalanceCommand>(),
            mockEventRecorder.Object,
            "yolodaily");

        // act
        await command.ExecuteAsync();

        // assert
        var factorEvent = rebalanceEvents.Single(e => e.EventType == "FactorsCalculated");
        factorEvent.Summary.ShouldBe("Calculated factor snapshots for 2 ticker(s)");

        using var payload = JsonDocument.Parse(factorEvent.PayloadJson);
        var rawItems = payload.RootElement.GetProperty("rawFactors").EnumerateArray().ToArray();
        rawItems[0].GetProperty("Ticker").GetString().ShouldBe("BTC/USDT");
        rawItems[0].GetProperty("Carry").GetDouble().ShouldBe(0.1d);
        rawItems[0].GetProperty("Volatility").GetDouble().ShouldBe(0.42d);
        rawItems[1].GetProperty("Ticker").GetString().ShouldBe("ETH/USDT");
        rawItems[1].GetProperty("Volatility").ValueKind.ShouldBe(JsonValueKind.Null);

        var normalizedItems = payload.RootElement.GetProperty("normalizedFactors").EnumerateArray().ToArray();
        normalizedItems[0].GetProperty("Carry").GetDouble().ShouldBe(1d);
        normalizedItems[1].GetProperty("Carry").GetDouble().ShouldBe(-1d);

        var weightsPayload = payload.RootElement.GetProperty("weights");
        weightsPayload.GetProperty("BTC/USDT").GetDecimal().ShouldBe(0.5m);
        weightsPayload.GetProperty("ETH/USDT").GetDecimal().ShouldBe(-0.25m);

        rebalanceEvents.Select(e => e.Sequence).ShouldBe(Enumerable.Range(1, rebalanceEvents.Count));
    }
}
