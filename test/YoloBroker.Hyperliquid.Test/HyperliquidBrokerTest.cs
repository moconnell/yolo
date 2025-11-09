using System.Net;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using HyperLiquid.Net.Enums;
using HyperLiquid.Net.Interfaces.Clients;
using HyperLiquid.Net.Interfaces.Clients.FuturesApi;
using HyperLiquid.Net.Interfaces.Clients.SpotApi;
using HyperLiquid.Net.Objects.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit.Abstractions;
using YoloAbstractions;
using YoloBroker.Hyperliquid.Exceptions;
using YoloTest.Util;
using static YoloAbstractions.AssetType;
using static YoloBroker.Hyperliquid.Test.TickerAliasUtil;
using static YoloBroker.Hyperliquid.Test.TradeUtil;
using OrderSide = YoloAbstractions.OrderSide;
using OrderStatus = YoloAbstractions.OrderStatus;
using HlOrderSide = HyperLiquid.Net.Enums.OrderSide;
using HlOrderStatus = HyperLiquid.Net.Enums.OrderStatus;

namespace YoloBroker.Hyperliquid.Test;

public class HyperliquidBrokerTest
{
    private readonly ILoggerFactory _loggerFactory;

    public HyperliquidBrokerTest(ITestOutputHelper testOutputHelper)
    {
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
        var broker = GetBrokerWithMockedClient();

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
        var broker = GetBrokerWithMockedClient();
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
        var broker = GetBrokerWithMockedClient();

        // act & assert
        await Should.ThrowAsync<ArgumentNullException>(() => broker.CancelOrderAsync(null!));
    }

    [Fact]
    public async Task GivenCompletedOrder_WhenCancelOrderAsync_ShouldSkipCancellation()
    {
        // arrange
        var broker = GetBrokerWithMockedClient();
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
        var broker = GetBrokerWithMockedClient();
        var trade = new Trade(
            "BTC",
            (AssetType)999, // Invalid asset type
            1m,
            50000m);

        // act & assert
        Should.ThrowAsync<ArgumentOutOfRangeException>(() => broker.PlaceTradeAsync(trade));
    }

    [Fact]
    public void GivenUnsupportedAssetType_WhenCancelOrder_ShouldThrowArgumentOutOfRangeException()
    {
        // arrange
        var broker = GetBrokerWithMockedClient();
        var order = new Order(
            123,
            "BTC",
            (AssetType)999, // Invalid asset type
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
        var broker = GetBrokerWithMockedClient();

        // act & assert
        await Should.ThrowAsync<ArgumentNullException>(() => broker.EditOrderAsync(null!));
    }

    [Fact]
    public async Task GivenNullOrEmptyTicker_WhenGetDailyClosePricesAsync_ShouldThrowArgumentException()
    {
        // arrange
        var broker = GetBrokerWithMockedClient();

        // act & assert
        await Should.ThrowAsync<ArgumentException>(() =>
            broker.GetDailyClosePricesAsync(null!, 10));
        await Should.ThrowAsync<ArgumentException>(() =>
            broker.GetDailyClosePricesAsync("", 10));
    }

    [Fact]
    public async Task GivenValidTicker_WhenGetDailyClosePricesAsync_ShouldReturnClosePrices()
    {
        // arrange
        var mockRestClient = new Mock<IHyperLiquidRestClient>();
        var mockSpotApi = new Mock<IHyperLiquidRestClientSpotApi>();
        var mockSpotExchangeData = new Mock<IHyperLiquidRestClientSpotApiExchangeData>();

        mockRestClient.Setup(x => x.SpotApi).Returns(mockSpotApi.Object);
        mockSpotApi.Setup(x => x.ExchangeData).Returns(mockSpotExchangeData.Object);

        var klines = new[]
        {
            new HyperLiquidKline
            {
                OpenTime = DateTime.UtcNow.AddDays(-3),
                ClosePrice = 1900m,
                OpenPrice = 1850m,
                HighPrice = 1950m,
                LowPrice = 1800m,
                Volume = 1000m
            },
            new HyperLiquidKline
            {
                OpenTime = DateTime.UtcNow.AddDays(-2),
                ClosePrice = 2000m,
                OpenPrice = 1900m,
                HighPrice = 2050m,
                LowPrice = 1850m,
                Volume = 1200m
            },
            new HyperLiquidKline
            {
                OpenTime = DateTime.UtcNow.AddDays(-1),
                ClosePrice = 2100m,
                OpenPrice = 2000m,
                HighPrice = 2150m,
                LowPrice = 1950m,
                Volume = 1500m
            }
        };

        mockSpotExchangeData
            .Setup(x => x.GetKlinesAsync(
                It.IsAny<string>(),
                KlineInterval.OneDay,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebCallResult(klines));

        var broker = GetBrokerWithMockedClient(mockRestClient.Object);

        // act
        var closePrices = await broker.GetDailyClosePricesAsync("ETH", 3);

        // assert
        closePrices.Count.ShouldBe(3);
        closePrices[0].ShouldBe(1900m);
        closePrices[1].ShouldBe(2000m);
        closePrices[2].ShouldBe(2100m);
    }

    [Fact]
    public async Task GivenTickerWithAlias_WhenGetDailyClosePricesAsync_ShouldUseAlias()
    {
        // arrange
        var mockRestClient = new Mock<IHyperLiquidRestClient>();
        var mockSpotApi = new Mock<IHyperLiquidRestClientSpotApi>();
        var mockSpotExchangeData = new Mock<IHyperLiquidRestClientSpotApiExchangeData>();

        mockRestClient.Setup(x => x.SpotApi).Returns(mockSpotApi.Object);
        mockSpotApi.Setup(x => x.ExchangeData).Returns(mockSpotExchangeData.Object);

        var klines = new[]
        {
            new HyperLiquidKline
            {
                OpenTime = DateTime.UtcNow.AddDays(-1),
                ClosePrice = 50000m,
                OpenPrice = 49000m,
                HighPrice = 51000m,
                LowPrice = 48000m,
                Volume = 500m
            }
        };

        mockSpotExchangeData
            .Setup(x => x.GetKlinesAsync(
                "BTC-PERP", // The alias
                KlineInterval.OneDay,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebCallResult(klines));

        var aliases = new Dictionary<string, string> { { "BTC", "BTC-PERP" } };
        var broker = GetBrokerWithMockedClient(mockRestClient.Object, aliases);

        // act
        var closePrices = await broker.GetDailyClosePricesAsync("BTC", 1);

        // assert
        closePrices.Count.ShouldBe(1);
        closePrices[0].ShouldBe(50000m);
        mockSpotExchangeData.Verify(
            x => x.GetKlinesAsync(
                "BTC-PERP",
                KlineInterval.OneDay,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void GivenBroker_WhenDisposedTwice_ShouldNotThrow()
    {
        // arrange
        var broker = GetBrokerWithMockedClient();

        // act
        broker.Dispose();

        // assert - should not throw
        broker.Dispose();
    }

    [Fact]
    public async Task GivenNullTrades_WhenManageOrdersAsync_ShouldThrowArgumentNullException()
    {
        // arrange
        var broker = GetBrokerWithMockedClient();

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
        var broker = GetBrokerWithMockedClient();
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
        var broker = GetBrokerWithMockedClient();
        var results = new List<OrderUpdate>();

        // act
        await foreach (var update in broker.ManageOrdersAsync([], OrderManagementSettings.Default))
        {
            results.Add(update);
        }

        // assert
        results.ShouldBeEmpty();
    }

    #region PlaceTradeAsync Tests

    [Fact]
    public async Task GivenSuccessfulSpotTrade_WhenPlaceTradeAsync_ShouldReturnSuccessResult()
    {
        // arrange
        var mockRestClient = new Mock<IHyperLiquidRestClient>();
        var mockSpotApi = new Mock<IHyperLiquidRestClientSpotApi>();
        var mockSpotTrading = new Mock<IHyperLiquidRestClientSpotApiTrading>();

        mockRestClient.Setup(x => x.SpotApi).Returns(mockSpotApi.Object);
        mockSpotApi.Setup(x => x.Trading).Returns(mockSpotTrading.Object);

        var trade = CreateTrade("ETH", Spot, 0.5, 2000m);
        var orderResult = new HyperLiquidOrderResult
        {
            OrderId = 12345,
            Status = HlOrderStatus.Filled,
            AveragePrice = 2000m,
            FilledQuantity = 0.5m
        };

        mockSpotTrading
            .Setup(x => x.PlaceOrderAsync(
                trade.Symbol,
                It.IsAny<HlOrderSide>(),
                It.IsAny<HyperLiquid.Net.Enums.OrderType>(),
                trade.AbsoluteAmount,
                trade.LimitPrice!.Value,
                null,
                null,
                trade.ClientOrderId,
                null,
                null,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebCallResult(orderResult));

        var broker = GetBrokerWithMockedClient(mockRestClient.Object);

        // act
        var result = await broker.PlaceTradeAsync(trade);

        // assert
        result.Success.ShouldBeTrue();
        result.Order.ShouldNotBeNull();
        result.Order!.Id.ShouldBe(12345);
        result.Order.Symbol.ShouldBe("ETH");
        result.Order.AssetType.ShouldBe(Spot);
        result.Order.OrderStatus.ShouldBe(OrderStatus.Filled);
        result.Order.Amount.ShouldBe(0.5m);
        result.Order.Filled.ShouldBe(0.5m);
    }

    [Fact]
    public async Task GivenSuccessfulFuturesTrade_WhenPlaceTradeAsync_ShouldReturnSuccessResult()
    {
        // arrange
        var mockRestClient = new Mock<IHyperLiquidRestClient>();
        var mockFuturesApi = new Mock<IHyperLiquidRestClientFuturesApi>();
        var mockFuturesTrading = new Mock<IHyperLiquidRestClientFuturesApiTrading>();

        mockRestClient.Setup(x => x.FuturesApi).Returns(mockFuturesApi.Object);
        mockFuturesApi.Setup(x => x.Trading).Returns(mockFuturesTrading.Object);

        var trade = CreateTrade("BTC", Future, 1.0, 50000m);
        var orderResult = new HyperLiquidOrderResult
        {
            OrderId = 67890,
            Status = HlOrderStatus.Open,
            AveragePrice = null,
            FilledQuantity = 0m
        };

        mockFuturesTrading
            .Setup(x => x.PlaceOrderAsync(
                trade.Symbol,
                It.IsAny<HlOrderSide>(),
                It.IsAny<HyperLiquid.Net.Enums.OrderType>(),
                trade.AbsoluteAmount,
                trade.LimitPrice!.Value,
                null,
                null,
                trade.ClientOrderId,
                null,
                null,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebCallResult(orderResult));

        var broker = GetBrokerWithMockedClient(mockRestClient.Object);

        // act
        var result = await broker.PlaceTradeAsync(trade);

        // assert
        result.Success.ShouldBeTrue();
        result.Order.ShouldNotBeNull();
        result.Order!.Id.ShouldBe(67890);
        result.Order.Symbol.ShouldBe("BTC");
        result.Order.AssetType.ShouldBe(Future);
        result.Order.OrderStatus.ShouldBe(OrderStatus.Open);
        result.Order.Amount.ShouldBe(1.0m);
        result.Order.Filled.ShouldBe(0m);
    }

    [Fact]
    public async Task GivenFailedSpotTrade_WhenPlaceTradeAsync_ShouldReturnFailureResult()
    {
        // arrange
        const int errorCode = 400;
        const string insufficientBalance = "Insufficient balance";

        var mockRestClient = new Mock<IHyperLiquidRestClient>();
        var mockSpotApi = new Mock<IHyperLiquidRestClientSpotApi>();
        var mockSpotTrading = new Mock<IHyperLiquidRestClientSpotApiTrading>();

        mockRestClient.Setup(x => x.SpotApi).Returns(mockSpotApi.Object);
        mockSpotApi.Setup(x => x.Trading).Returns(mockSpotTrading.Object);

        var trade = CreateTrade("ETH", Spot, 0.5, 2000m);
        var error = new TestError(
            errorCode.ToString(),
            new ErrorInfo(ErrorType.InsufficientBalance, insufficientBalance) { Message = insufficientBalance },
            new Exception(insufficientBalance));

        mockSpotTrading
            .Setup(x => x.PlaceOrderAsync(
                It.IsAny<string>(),
                It.IsAny<HlOrderSide>(),
                It.IsAny<HyperLiquid.Net.Enums.OrderType>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                null,
                null,
                It.IsAny<string?>(),
                null,
                null,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                WebCallResult<HyperLiquidOrderResult>(
                    null,
                    HttpStatusCode.BadRequest,
                    error));

        var broker = GetBrokerWithMockedClient(mockRestClient.Object);

        // act
        var result = await broker.PlaceTradeAsync(trade);

        // assert
        result.Success.ShouldBeFalse();
        result.Order.ShouldBeNull();
        result.Error.ShouldBe(insufficientBalance);
        result.ErrorCode.ShouldBe(errorCode);
    }

    #endregion

    #region PlaceTradesAsync Tests

    [Fact]
    public async Task GivenMultipleSpotTrades_WhenPlaceTradesAsync_ShouldReturnAllResults()
    {
        // arrange
        var mockRestClient = new Mock<IHyperLiquidRestClient>();
        var mockSpotApi = new Mock<IHyperLiquidRestClientSpotApi>();
        var mockSpotTrading = new Mock<IHyperLiquidRestClientSpotApiTrading>();

        mockRestClient.Setup(x => x.SpotApi).Returns(mockSpotApi.Object);
        mockSpotApi.Setup(x => x.Trading).Returns(mockSpotTrading.Object);

        var trades = new[]
        {
            CreateTrade("ETH", Spot, 0.5, 2000m),
            CreateTrade("BTC", Spot, 0.1, 50000m)
        };

        var orderResults = new[]
        {
            new CallResult<HyperLiquidOrderResult>(
                new HyperLiquidOrderResult
                {
                    OrderId = 111,
                    Status = HlOrderStatus.Filled,
                    FilledQuantity = 0.5m
                }),
            new CallResult<HyperLiquidOrderResult>(
                new HyperLiquidOrderResult
                {
                    OrderId = 222,
                    Status = HlOrderStatus.Open,
                    FilledQuantity = 0m
                })
        };

        mockSpotTrading
            .Setup(x => x.PlaceMultipleOrdersAsync(
                It.IsAny<IEnumerable<HyperLiquidOrderRequest>>(),
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebCallResult(orderResults));

        var broker = GetBrokerWithMockedClient(mockRestClient.Object);

        // act
        var results = new List<TradeResult>();
        await foreach (var result in broker.PlaceTradesAsync(trades))
        {
            results.Add(result);
        }

        // assert
        results.Count.ShouldBe(2);
        results[0].Success.ShouldBeTrue();
        results[0].Order!.Id.ShouldBe(111);
        results[1].Success.ShouldBeTrue();
        results[1].Order!.Id.ShouldBe(222);
    }

    [Fact]
    public async Task GivenMultipleFuturesTrades_WhenPlaceTradesAsync_ShouldReturnAllResults()
    {
        // arrange
        var mockRestClient = new Mock<IHyperLiquidRestClient>();
        var mockFuturesApi = new Mock<IHyperLiquidRestClientFuturesApi>();
        var mockFuturesTrading = new Mock<IHyperLiquidRestClientFuturesApiTrading>();

        mockRestClient.Setup(x => x.FuturesApi).Returns(mockFuturesApi.Object);
        mockFuturesApi.Setup(x => x.Trading).Returns(mockFuturesTrading.Object);

        var trades = new[]
        {
            CreateTrade("ETH", Future, 1.0, 2000m),
            CreateTrade("BTC", Future, 0.5, 50000m)
        };

        var orderResults = new[]
        {
            new CallResult<HyperLiquidOrderResult>(
                new HyperLiquidOrderResult
                {
                    OrderId = 333,
                    Status = HlOrderStatus.Open,
                    FilledQuantity = 0m
                }),
            new CallResult<HyperLiquidOrderResult>(
                new HyperLiquidOrderResult
                {
                    OrderId = 444,
                    Status = HlOrderStatus.Filled,
                    FilledQuantity = 0.25m
                })
        };

        mockFuturesTrading
            .Setup(x => x.PlaceMultipleOrdersAsync(
                It.IsAny<IEnumerable<HyperLiquidOrderRequest>>(),
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebCallResult(orderResults));

        var broker = GetBrokerWithMockedClient(mockRestClient.Object);

        // act
        var results = new List<TradeResult>();
        await foreach (var result in broker.PlaceTradesAsync(trades))
        {
            results.Add(result);
        }

        // assert
        results.Count.ShouldBe(2);
        results[0].Success.ShouldBeTrue();
        results[0].Order!.Id.ShouldBe(333);
        results[1].Success.ShouldBeTrue();
        results[1].Order!.Id.ShouldBe(444);
    }

    [Fact]
    public async Task GivenMixedSpotAndFuturesTrades_WhenPlaceTradesAsync_ShouldReturnAllResults()
    {
        // arrange
        var mockRestClient = new Mock<IHyperLiquidRestClient>();
        var mockSpotApi = new Mock<IHyperLiquidRestClientSpotApi>();
        var mockSpotTrading = new Mock<IHyperLiquidRestClientSpotApiTrading>();
        var mockFuturesApi = new Mock<IHyperLiquidRestClientFuturesApi>();
        var mockFuturesTrading = new Mock<IHyperLiquidRestClientFuturesApiTrading>();

        mockRestClient.Setup(x => x.SpotApi).Returns(mockSpotApi.Object);
        mockSpotApi.Setup(x => x.Trading).Returns(mockSpotTrading.Object);
        mockRestClient.Setup(x => x.FuturesApi).Returns(mockFuturesApi.Object);
        mockFuturesApi.Setup(x => x.Trading).Returns(mockFuturesTrading.Object);

        var trades = new[]
        {
            CreateTrade("ETH", Spot, 0.5, 2000m),
            CreateTrade("BTC", Future, 1.0, 50000m)
        };

        var spotOrderResults = new[]
        {
            new CallResult<HyperLiquidOrderResult>(
                new HyperLiquidOrderResult
                {
                    OrderId = 555,
                    Status = HlOrderStatus.Filled,
                    FilledQuantity = 0.5m
                })
        };

        var futuresOrderResults = new[]
        {
            new CallResult<HyperLiquidOrderResult>(
                new HyperLiquidOrderResult
                {
                    OrderId = 666,
                    Status = HlOrderStatus.Open,
                    FilledQuantity = 0m
                })
        };

        mockSpotTrading
            .Setup(x => x.PlaceMultipleOrdersAsync(
                It.IsAny<IEnumerable<HyperLiquidOrderRequest>>(),
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebCallResult(spotOrderResults));

        mockFuturesTrading
            .Setup(x => x.PlaceMultipleOrdersAsync(
                It.IsAny<IEnumerable<HyperLiquidOrderRequest>>(),
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebCallResult(futuresOrderResults));

        var broker = GetBrokerWithMockedClient(mockRestClient.Object);

        // act
        var results = new List<TradeResult>();
        await foreach (var result in broker.PlaceTradesAsync(trades))
        {
            results.Add(result);
        }

        // assert
        results.Count.ShouldBe(2);
        results[0].Success.ShouldBeTrue();
        results[0].Order!.Id.ShouldBe(555);
        results[0].Trade.AssetType.ShouldBe(Spot);
        results[1].Success.ShouldBeTrue();
        results[1].Order!.Id.ShouldBe(666);
        results[1].Trade.AssetType.ShouldBe(Future);
    }

    #endregion

    #region GetOpenOrdersAsync Tests

    [Fact]
    public async Task GivenOpenOrders_WhenGetOpenOrdersAsync_ShouldReturnMappedOrders()
    {
        // arrange
        var mockRestClient = new Mock<IHyperLiquidRestClient>();
        var mockFuturesApi = new Mock<IHyperLiquidRestClientFuturesApi>();
        var mockFuturesTrading = new Mock<IHyperLiquidRestClientFuturesApiTrading>();

        mockRestClient.Setup(x => x.FuturesApi).Returns(mockFuturesApi.Object);
        mockFuturesApi.Setup(x => x.Trading).Returns(mockFuturesTrading.Object);

        var hlOrders = new[]
        {
            new HyperLiquidOrder
            {
                OrderId = 123,
                Symbol = "ETH",
                SymbolType = SymbolType.Spot,
                Timestamp = DateTime.UtcNow.AddMinutes(-5),
                OrderSide = HlOrderSide.Buy,
                Quantity = 1.0m,
                QuantityRemaining = 0.5m,
                Price = 2000m,
                ClientOrderId = "client123"
            },
            new HyperLiquidOrder
            {
                OrderId = 456,
                Symbol = "BTC",
                SymbolType = SymbolType.Futures,
                Timestamp = DateTime.UtcNow.AddMinutes(-10),
                OrderSide = HlOrderSide.Sell,
                Quantity = 0.5m,
                QuantityRemaining = 0.5m,
                Price = 50000m,
                ClientOrderId = "client456"
            }
        };

        mockFuturesTrading
            .Setup(x => x.GetOpenOrdersExtendedAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebCallResult(hlOrders));

        var broker = GetBrokerWithMockedClient(mockRestClient.Object);

        // act
        var orders = await broker.GetOpenOrdersAsync();

        // assert
        orders.Count.ShouldBe(2);
        orders[123].Symbol.ShouldBe("ETH");
        orders[123].AssetType.ShouldBe(Spot);
        orders[123].OrderSide.ShouldBe(OrderSide.Buy);
        orders[123].Amount.ShouldBe(1.0m);
        orders[456].Symbol.ShouldBe("BTC");
        orders[456].AssetType.ShouldBe(Future);
        orders[456].OrderSide.ShouldBe(OrderSide.Sell);
    }

    [Fact]
    public async Task GivenNoOpenOrders_WhenGetOpenOrdersAsync_ShouldReturnEmptyDictionary()
    {
        // arrange
        var mockRestClient = new Mock<IHyperLiquidRestClient>();
        var mockFuturesApi = new Mock<IHyperLiquidRestClientFuturesApi>();
        var mockFuturesTrading = new Mock<IHyperLiquidRestClientFuturesApiTrading>();

        mockRestClient.Setup(x => x.FuturesApi).Returns(mockFuturesApi.Object);
        mockFuturesApi.Setup(x => x.Trading).Returns(mockFuturesTrading.Object);

        mockFuturesTrading
            .Setup(x => x.GetOpenOrdersExtendedAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebCallResult(Array.Empty<HyperLiquidOrder>()));

        var broker = GetBrokerWithMockedClient(mockRestClient.Object);

        // act
        var orders = await broker.GetOpenOrdersAsync();

        // assert
        orders.ShouldBeEmpty();
    }

    [Fact]
    public async Task GivenApiError_WhenGetOpenOrdersAsync_ShouldThrowException()
    {
        // arrange
        var mockRestClient = new Mock<IHyperLiquidRestClient>();
        var mockFuturesApi = new Mock<IHyperLiquidRestClientFuturesApi>();
        var mockFuturesTrading = new Mock<IHyperLiquidRestClientFuturesApiTrading>();

        mockRestClient.Setup(x => x.FuturesApi).Returns(mockFuturesApi.Object);
        mockFuturesApi.Setup(x => x.Trading).Returns(mockFuturesTrading.Object);

        var error = new TestError("500", new ErrorInfo(ErrorType.SystemError, "Internal server error"), null);
        mockFuturesTrading
            .Setup(x => x.GetOpenOrdersExtendedAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                WebCallResult<HyperLiquidOrder[]>(
                    null,
                    HttpStatusCode.InternalServerError,
                    error));

        var broker = GetBrokerWithMockedClient(mockRestClient.Object);

        // act & assert
        await Should.ThrowAsync<HyperliquidException>(() => broker.GetOpenOrdersAsync());
    }

    #endregion

    #region CancelOrderAsync Tests

    [Fact]
    public async Task GivenOpenSpotOrder_WhenCancelOrderAsync_ShouldCallApi()
    {
        // arrange
        var mockRestClient = new Mock<IHyperLiquidRestClient>();
        var mockSpotApi = new Mock<IHyperLiquidRestClientSpotApi>();
        var mockSpotTrading = new Mock<IHyperLiquidRestClientSpotApiTrading>();

        mockRestClient.Setup(x => x.SpotApi).Returns(mockSpotApi.Object);
        mockSpotApi.Setup(x => x.Trading).Returns(mockSpotTrading.Object);

        var order = new Order(
            123,
            "ETH",
            Spot,
            DateTime.UtcNow,
            OrderSide.Buy,
            OrderStatus.Open,
            1m,
            0m,
            2000m,
            "test");

        mockSpotTrading
            .Setup(x => x.CancelOrderAsync(
                order.Symbol,
                order.Id,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebCallResult());

        var broker = GetBrokerWithMockedClient(mockRestClient.Object);

        // act
        await broker.CancelOrderAsync(order);

        // assert
        mockSpotTrading.Verify(
            x => x.CancelOrderAsync(order.Symbol, order.Id, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GivenOpenFuturesOrder_WhenCancelOrderAsync_ShouldCallApi()
    {
        // arrange
        var mockRestClient = new Mock<IHyperLiquidRestClient>();
        var mockFuturesApi = new Mock<IHyperLiquidRestClientFuturesApi>();
        var mockFuturesTrading = new Mock<IHyperLiquidRestClientFuturesApiTrading>();

        mockRestClient.Setup(x => x.FuturesApi).Returns(mockFuturesApi.Object);
        mockFuturesApi.Setup(x => x.Trading).Returns(mockFuturesTrading.Object);

        var order = new Order(
            456,
            "BTC",
            Future,
            DateTime.UtcNow,
            OrderSide.Sell,
            OrderStatus.Open,
            0.5m,
            0m,
            50000m,
            "test");

        mockFuturesTrading
            .Setup(x => x.CancelOrderAsync(
                order.Symbol,
                order.Id,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebCallResult());

        var broker = GetBrokerWithMockedClient(mockRestClient.Object);

        // act
        await broker.CancelOrderAsync(order);

        // assert
        mockFuturesTrading.Verify(
            x => x.CancelOrderAsync(order.Symbol, order.Id, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region EditOrderAsync Tests

    [Fact]
    public async Task GivenOrder_WhenEditOrderAsync_ShouldCallApi()
    {
        // arrange
        var mockRestClient = new Mock<IHyperLiquidRestClient>();
        var mockFuturesApi = new Mock<IHyperLiquidRestClientFuturesApi>();
        var mockFuturesTrading = new Mock<IHyperLiquidRestClientFuturesApiTrading>();

        mockRestClient.Setup(x => x.FuturesApi).Returns(mockFuturesApi.Object);
        mockFuturesApi.Setup(x => x.Trading).Returns(mockFuturesTrading.Object);

        var order = new Order(
            789,
            "ETH",
            Future,
            DateTime.UtcNow,
            OrderSide.Buy,
            OrderStatus.Open,
            2m,
            0m,
            2100m,
            "client789");

        mockFuturesTrading
            .Setup(x => x.EditOrderAsync(
                order.Symbol,
                order.Id,
                order.ClientId,
                It.IsAny<HlOrderSide>(),
                It.IsAny<HyperLiquid.Net.Enums.OrderType>(),
                order.Amount,
                order.LimitPrice!.Value,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebCallResult());

        var broker = GetBrokerWithMockedClient(mockRestClient.Object);

        // act
        await broker.EditOrderAsync(order);

        // assert
        mockFuturesTrading.Verify(
            x => x.EditOrderAsync(
                order.Symbol,
                order.Id,
                order.ClientId,
                It.IsAny<HlOrderSide>(),
                It.IsAny<HyperLiquid.Net.Enums.OrderType>(),
                order.Amount,
                order.LimitPrice!.Value,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    private HyperliquidBroker GetBrokerWithMockedClient(
        IHyperLiquidRestClient? restClient = null,
        IReadOnlyDictionary<string, string>? aliases = null)
    {
        restClient ??= new Mock<IHyperLiquidRestClient>().Object;
        var mockSocketClient = new Mock<IHyperLiquidSocketClient>();
        var logger = _loggerFactory.CreateLogger<HyperliquidBroker>();

        return new HyperliquidBroker(
            restClient,
            mockSocketClient.Object,
            GetTickerAliasService(aliases),
            logger);
    }

    private static WebCallResult WebCallResult(
        HttpStatusCode httpStatusCode = HttpStatusCode.OK,
        Error? error = null) =>
        new(
            httpStatusCode,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            error);


    private static WebCallResult<T> WebCallResult<T>(
        T? result,
        HttpStatusCode httpStatusCode = HttpStatusCode.OK,
        Error? error = null) =>
        new(
            httpStatusCode,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            ResultDataSource.Server,
            result,
            error);

    private class TestError(string? errorCode, ErrorInfo errorInfo, Exception? exception)
        : Error(
            errorCode,
            errorInfo,
            exception);
}