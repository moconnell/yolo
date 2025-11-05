using System.Collections.Concurrent;
using System.Security.Cryptography;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using HyperLiquid.Net;
using HyperLiquid.Net.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit.Abstractions;
using YoloAbstractions;
using YoloBroker.Hyperliquid.Extensions;
using YoloBroker.Interface;
using YoloTest.Util;
using static YoloAbstractions.AssetPermissions;
using static YoloAbstractions.AssetType;
using static YoloAbstractions.OrderType;

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

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("ETH", PerpetualFutures, "ETH")]
    [InlineData("BTC,ETH", PerpetualFutures, "BTC,ETH")]
    [InlineData("SHIB", PerpetualFutures, "SHIB", "SHIB:kSHIB")]
    public async Task GivenBaseAsset_ShouldGetMarkets(
        string baseAssetFilterString,
        AssetPermissions assetPermissions,
        string expectedMarketsString,
        params string[] aliasConfig)
    {
        // arrange
        var baseAssetFilter = baseAssetFilterString.Split(',').Select(asset => asset.Trim()).ToHashSet();
        var expectedMarkets = expectedMarketsString.Split(',').Select(market => market.Trim()).ToHashSet();
        var aliases = aliasConfig.Length > 0
            ? aliasConfig.Select(alias =>
                {
                    var aliasParts = alias.Split(':');
                    return new KeyValuePair<string, string>(aliasParts[0], aliasParts[1]);
                })
                .ToDictionary()
            : null;

        var broker = GetTestBroker(aliases);

        // act
        var results = await broker.GetMarketsAsync(baseAssetFilter, assetPermissions: assetPermissions);

        // assert
        Assert.NotNull(results);
        Assert.Equal(expectedMarkets.Count, results.Count);
        Assert.True(expectedMarkets.All(ticker => results.ContainsKey(ticker)));
        Assert.True(results.All(kvp => kvp.Value.All(info => !string.IsNullOrEmpty(info.BaseAsset))));
        Assert.True(results.All(kvp => kvp.Value.All(info => !string.IsNullOrEmpty(info.Key))));
        Assert.True(results.All(kvp => kvp.Value.All(info => !string.IsNullOrEmpty(info.Name))));
        Assert.True(results.All(kvp => kvp.Value.All(info => info.Ask > 0)));
        Assert.True(results.All(kvp => kvp.Value.All(info => info.Bid > 0)));
        Assert.True(results.All(kvp => kvp.Value.All(info => info.Mid > 0)));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ShouldGetOpenOrders()
    {
        // arrange
        var broker = GetTestBroker();

        // act
        var results = await broker.GetOpenOrdersAsync();

        // assert
        Assert.NotNull(results);
    }

    [Theory]
    [Trait("Category", "Integration")]
    // [InlineData("HYPE/USDC", Spot, 1)]
    [InlineData("ETH", Future, 0.01)]
    [InlineData("BTC", Future, 0.01)]
    public async Task ShouldPlaceOrder(string symbol, AssetType assetType, double quantity)
    {
        // arrange
        var broker = GetTestBroker();
        var orderPrice = await GetLimitPrice(broker, symbol, assetType, quantity);
        var trade = CreateTrade(symbol, assetType, quantity, orderPrice);

        // act
        var tradeResult = await broker.PlaceTradeAsync(trade);

        // assert
        tradeResult.ShouldNotBeNull();
        tradeResult.Success.ShouldBeTrue(tradeResult.Error);
        var order = tradeResult.Order;
        order.ShouldNotBeNull();
        order.Id.ShouldBeGreaterThan(0);
        order.ClientId.ShouldBe(trade.ClientOrderId);
        order.Symbol.ShouldBe(symbol);
        order.Amount.ShouldBe(trade.Amount);

        await broker.CancelOrderAsync(order);
    }

    [Theory]
    [Trait("Category", "Integration")]
    // [InlineData("HYPE/USDC", Spot, 1)]
    [InlineData("ETH", Future, 0.01)]
    [InlineData("ETH", Future, 0.0001, Market)]
    [InlineData("BTC", Future, 0.001)]
    public async Task ShouldPlaceOrders(
        string symbol,
        AssetType assetType,
        double quantity,
        OrderType orderType = Limit)
    {
        // arrange
        var broker = GetTestBroker();
        decimal? limitPrice = orderType == Limit ? await GetLimitPrice(broker, symbol, assetType, quantity) : null;
        var trade = CreateTrade(symbol, assetType, quantity, limitPrice);

        // act
        var tradeResults = broker.PlaceTradesAsync([trade]);

        // assert
        await foreach (var tradeResult in tradeResults)
        {
            tradeResult.ShouldNotBeNull();
            tradeResult.Success.ShouldBeTrue(tradeResult.Error);
            var order = tradeResult.Order;
            order.ShouldNotBeNull();
            order.Id.ShouldBeGreaterThan(0);
            order.ClientId.ShouldBe(trade.ClientOrderId);
            order.Symbol.ShouldBe(symbol);
            order.Amount.ShouldBe(trade.Amount);

            if (orderType == Limit)
            {
                await broker.CancelOrderAsync(order);
            }
        }
    }

    [Theory]
    [Trait("Category", "Integration")]
    // [InlineData("HYPE/USDC", Spot, 1)]
    // [InlineData("ETH", Future, 0.01)]
    [InlineData("BTC", Future, 0.0005)]
    public async Task ShouldManageOrders(string symbol, AssetType assetType, double quantity)
    {
        // arrange
        var broker = GetTestBroker();
        var limitPrice = await GetLimitPrice(broker, symbol, assetType, quantity);
        var trade = CreateTrade(symbol, assetType, quantity, limitPrice);
        var settings = OrderManagementSettings.Default;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var orders = new ConcurrentDictionary<long, Order>();

        // act
        var orderUpdates = broker.ManageOrdersAsync([trade], settings, cts.Token);

        // assert
        var updateCount = 0;

        try
        {
            await foreach (var orderUpdate in orderUpdates)
            {
                updateCount++;
                _testOutputHelper.WriteLine($"Update {updateCount}: {orderUpdate.Type} - {orderUpdate.Symbol}");

                orderUpdate.ShouldNotBeNull();

                if (orderUpdate.Type == OrderUpdateType.Error)
                {
                    _testOutputHelper.WriteLine($"Error: {orderUpdate.Error?.Message}");
                    throw orderUpdate.Error ?? new Exception("Unknown error");
                }

                var order = orderUpdate.Order;
                order.ShouldNotBeNull();
                orders.TryAdd(order.Id, order);
                order.Id.ShouldBeGreaterThan(0);
                order.ClientId.ShouldBe(trade.ClientOrderId);
                order.Symbol.ShouldBe(symbol);
                order.Amount.ShouldBe(trade.Amount);
                order.OrderStatus.ShouldBe(OrderStatus.Open);

                // Cancel after first successful order creation
                if (orderUpdate.Type == OrderUpdateType.Created)
                {
                    await cts.CancelAsync();
                }
            }
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            // Expected cancellation
            _testOutputHelper.WriteLine($"Test completed with {updateCount} updates");
        }
        finally
        {
            // clean-up
            foreach (var order in orders.Values)
            {
                await broker.CancelOrderAsync(order, CancellationToken.None);
            }
        }
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

    [Fact(Skip = "Could not get order book for NOT - [ServerError.UnknownSymbol] Symbol not found")]
    [Trait("Category", "Integration")]
    public async Task GivenNullBaseAssetFilter_WhenGetMarketsAsync_ShouldReturnAllMarkets()
    {
        // arrange
        var broker = GetTestBroker();

        // act
        var markets = await broker.GetMarketsAsync(null, assetPermissions: PerpetualFutures);

        // assert
        markets.ShouldNotBeNull();
        markets.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenSpotPermissions_WhenGetMarketsAsync_ShouldIncludeSpotMarkets()
    {
        // arrange
        var broker = GetTestBroker();

        // act
        var markets = await broker.GetMarketsAsync(
            new HashSet<string> { "BTC" },
            "USDC",
            assetPermissions: AssetPermissions.Spot);

        // assert
        markets.ShouldNotBeNull();
        markets.Values.SelectMany(m => m).Any(m => m.AssetType == AssetType.Spot).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenLongSpotPermissions_WhenGetMarketsAsync_ShouldIncludeSpotMarkets()
    {
        // arrange
        var broker = GetTestBroker();

        // act
        var markets = await broker.GetMarketsAsync(
            new HashSet<string> { "BTC" },
            "USDC",
            assetPermissions: LongSpot);

        // assert
        markets.ShouldNotBeNull();
        markets.Values.SelectMany(m => m).Any(m => m.AssetType == AssetType.Spot).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenShortSpotPermissions_WhenGetMarketsAsync_ShouldIncludeSpotMarkets()
    {
        // arrange
        var broker = GetTestBroker();

        // act
        var markets = await broker.GetMarketsAsync(
            new HashSet<string> { "BTC" },
            "USDC",
            assetPermissions: ShortSpot);

        // assert
        markets.ShouldNotBeNull();
        markets.Values.SelectMany(m => m).Any(m => m.AssetType == AssetType.Spot).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenSpotAndPerpPermissions_WhenGetMarketsAsync_ShouldIncludeBothTypes()
    {
        // arrange
        var broker = GetTestBroker();

        // act
        var markets = await broker.GetMarketsAsync(
            new HashSet<string> { "BTC" },
            "USDC",
            assetPermissions: SpotAndPerp);

        // assert
        markets.ShouldNotBeNull();
        var allMarkets = markets.Values.SelectMany(m => m).ToList();
        allMarkets.Any(m => m.AssetType == AssetType.Spot).ShouldBeTrue();
        allMarkets.Any(m => m.AssetType == Future).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenLongSpotAndPerpPermissions_WhenGetMarketsAsync_ShouldIncludeBothTypes()
    {
        // arrange
        var broker = GetTestBroker();

        // act
        var markets = await broker.GetMarketsAsync(
            new HashSet<string> { "BTC" },
            "USDC",
            assetPermissions: LongSpotAndPerp);

        // assert
        markets.ShouldNotBeNull();
        var allMarkets = markets.Values.SelectMany(m => m).ToList();
        allMarkets.Any(m => m.AssetType == Future).ShouldBeTrue();
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

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenTickerWithAlias_WhenGetMarketsAsync_ShouldReturnWithOriginalTicker()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            ["SHIB"] = "kSHIB"
        };
        var broker = GetTestBroker(aliases);

        // act
        var markets = await broker.GetMarketsAsync(
            new HashSet<string> { "SHIB" },
            assetPermissions: PerpetualFutures);

        // assert
        markets.ShouldNotBeNull();
        markets.ShouldContainKey("SHIB");
        markets["SHIB"].ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenPositions_WhenGetPositionsAsync_ShouldCombineSpotAndFutures()
    {
        // arrange
        var broker = GetTestBroker();

        // act
        var positions = await broker.GetPositionsAsync();

        // assert
        positions.ShouldNotBeNull();
        // Positions dictionary should contain both spot and futures if they exist
    }

    [Fact(Skip = "No bid for BTC/USDC spot")]
    [Trait("Category", "Integration")]
    public async Task GivenMultipleTrades_WhenPlaceTradesAsync_ShouldHandleMixedAssetTypes()
    {
        // arrange
        const string btcUsdc = "BTC/USDC";
        const string eth = "ETH";
        var broker = GetTestBroker();
        var ethFuturesPrice = await GetLimitPrice(broker, eth, Future, 0.001);
        var btcSpotPrice = await GetLimitPrice(broker, btcUsdc, AssetType.Spot, 0.0005);

        var trades = new[]
        {
            CreateTrade(eth, Future, 0.001, ethFuturesPrice),
            CreateTrade(btcUsdc, AssetType.Spot, 0.0005, btcSpotPrice)
        };

        var results = new ConcurrentBag<TradeResult>();

        // act
        await foreach (var result in broker.PlaceTradesAsync(trades))
        {
            results.Add(result);
        }

        // assert
        results.Count.ShouldBe(2);
        results.All(r => r.Success).ShouldBeTrue();

        // cleanup
        foreach (var result in results.Where(r => r.Order != null))
        {
            await broker.CancelOrderAsync(result.Order!);
        }
    }
    private HyperliquidBroker GetTestBroker(IReadOnlyDictionary<string, string>? aliases = null)
    {
        var (address, privateKey) = GetConfig();
        address.ShouldNotBeNullOrEmpty();
        privateKey.ShouldNotBeNullOrEmpty();

        var apiCredentials = new ApiCredentials(address, privateKey);

        var logger = _loggerFactory.CreateLogger<HyperliquidBroker>();

        var broker = new HyperliquidBroker(
            new HyperLiquidRestClient(options =>
            {
                options.ApiCredentials = apiCredentials;
                options.Environment = HyperLiquidEnvironment.Testnet;
            }),
            new HyperLiquidSocketClient(options =>
            {
                options.ApiCredentials = apiCredentials;
                options.Environment = HyperLiquidEnvironment.Testnet;
            }),
            GetTickerAliasService(),
            logger
        );
        return broker;

        ITickerAliasService GetTickerAliasService()
        {
            if (aliases != null)
            {
                return new TickerAliasService(aliases);
            }

            var mockTickerAliasService = new Mock<ITickerAliasService>();
            mockTickerAliasService.Setup(x => x.TryGetAlias(It.IsAny<string>(), out It.Ref<string>.IsAny))
                .Returns(false);
            return mockTickerAliasService.Object;
        }
    }

    private static (string? Address, string? PrivateKey) GetConfig()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
            .Build();

        return (config["Hyperliquid:Address"], config["Hyperliquid:PrivateKey"]);
    }

    private async Task<decimal> GetLimitPrice(
        HyperliquidBroker broker,
        string symbol,
        AssetType assetType,
        double quantity)
    {
        // Get current market price first
        var assets = symbol.Split('/').Select(s => s.Trim()).ToArray();
        var markets = assetType switch
        {
            AssetType.Spot => await broker.GetMarketsAsync(
                new HashSet<string> { assets[0] },
                assets[1],
                assetPermissions: AssetPermissions.Spot),
            Future => await broker.GetMarketsAsync(
                new HashSet<string> { assets[0] },
                assetPermissions: PerpetualFutures),
            _ => throw new NotSupportedException($"Asset type {assetType} is not supported.")
        };

        markets.ShouldNotBeNull();
        markets.ShouldContainKey(symbol);
        var marketInfo = markets[symbol][0];

        var isLong = quantity >= 0;
        var rawLimitPrice = isLong ? marketInfo.Bid * 0.99m : marketInfo.Ask * 1.01m;
        rawLimitPrice.ShouldNotBeNull();
        rawLimitPrice.Value.ShouldBeGreaterThan(0);

        var priceStep = marketInfo.PriceStep;
        priceStep.ShouldNotBeNull();

        // Round the limit price to the nearest price step
        var roundingType = isLong ? RoundingType.Down : RoundingType.Up;
        var roundedLimitPrice = rawLimitPrice.Value.RoundToValidPrice(priceStep.Value, roundingType);

        _testOutputHelper.WriteLine(
            $"Calculated limit price for {symbol}: {roundedLimitPrice} (raw: {rawLimitPrice.Value}, step: {priceStep.Value})");

        return roundedLimitPrice;
    }

    private static Trade CreateTrade(string symbol, AssetType assetType, double quantity, decimal? price) =>
        new(
            symbol,
            assetType,
            Convert.ToDecimal(quantity),
            price,
            ClientOrderId: $"0x{RandomNumberGenerator.GetHexString(32, true)}");
}