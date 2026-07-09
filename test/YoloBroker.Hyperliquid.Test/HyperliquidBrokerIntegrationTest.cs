using System.Collections.Concurrent;
using System.Security.Cryptography;
using CryptoExchange.Net.Objects;
using HyperLiquid.Net;
using HyperLiquid.Net.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using YoloAbstractions;
using YoloBroker.Hyperliquid.Extensions;
using YoloTest.Util;
using static YoloBroker.Hyperliquid.Test.TickerAliasUtil;
using static YoloBroker.Hyperliquid.Test.TradeUtil;

namespace YoloBroker.Hyperliquid.Test;

public class HyperliquidBrokerIntegrationTest : IAsyncLifetime
{
    private const bool IsTestnet = true;
    private static readonly bool FlattenPositionsOnExit = true;
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ILoggerFactory _loggerFactory;

    public HyperliquidBrokerIntegrationTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XUnitLoggerProvider(testOutputHelper));
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (!FlattenPositionsOnExit)
            return;

        var broker = GetTestBrokerForCleanup();
        if (broker is null)
            return;

        try
        {
            var cleanupErrors = new List<Exception>();
            await CancelOpenOrdersAsync(broker, cleanupErrors);
            await FlattenFuturesPositionsAsync(broker, cleanupErrors);

            if (cleanupErrors.Count != 0)
                throw new AggregateException("Failed to clean up Hyperliquid integration-test account", cleanupErrors);
        }
        catch (Exception ex)
        {
            _testOutputHelper.WriteLine($"Failed to flatten Hyperliquid integration-test account: {ex}");
            throw;
        }
        finally
        {
            broker.Dispose();
        }
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("ETH", AssetPermissions.PerpetualFutures, "ETH")]
    [InlineData("BTC,ETH", AssetPermissions.PerpetualFutures, "BTC,ETH")]
    [InlineData("SHIB", AssetPermissions.PerpetualFutures, "SHIB", "SHIB:kSHIB")]
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
    // [InlineData("ETH", AssetType.Future, 0.01)]
    [InlineData("BTC", AssetType.Future)]
    public async Task ShouldPlaceOrder(string symbol, AssetType assetType)
    {
        // arrange
        var broker = GetTestBroker();
        var marketInfo = await GetMarketInfo(broker, symbol, assetType);
        var quantity = GetTestOrderQuantity(marketInfo);
        var orderPrice = GetLimitPrice(symbol, quantity, marketInfo);
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
    [InlineData("ETH", AssetType.Future)]
    [InlineData("ETH", AssetType.Future, OrderType.Market)]
    [InlineData("BTC", AssetType.Future)]
    public async Task ShouldPlaceOrders(
        string symbol,
        AssetType assetType,
        OrderType orderType = OrderType.Limit)
    {
        // arrange
        var broker = GetTestBroker();
        var marketInfo = await GetMarketInfo(broker, symbol, assetType);
        var quantity = GetTestOrderQuantity(marketInfo);
        var limitPrice = GetLimitPrice(symbol, quantity, marketInfo);
        var trade = CreateTrade(symbol, assetType, quantity, limitPrice, orderType);

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

            if (orderType == OrderType.Limit)
            {
                await broker.CancelOrderAsync(order);
            }
        }
    }

    [Fact(Skip = "Could not get order book for NOT - [ServerError.UnknownSymbol] Symbol not found")]
    [Trait("Category", "Integration")]
    public async Task GivenNullBaseAssetFilter_WhenGetMarketsAsync_ShouldReturnAllMarkets()
    {
        // arrange
        var broker = GetTestBroker();

        // act
        var markets = await broker.GetMarketsAsync(assetPermissions: AssetPermissions.PerpetualFutures);

        // assert
        markets.ShouldNotBeNull();
        markets.Count.ShouldBeGreaterThan(0);
    }

    [Fact(Skip = "Spot tests failing - invalid data from Hyperliquid")]
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

    [Fact(Skip = "Spot tests failing - invalid data from Hyperliquid")]
    [Trait("Category", "Integration")]
    public async Task GivenLongSpotPermissions_WhenGetMarketsAsync_ShouldIncludeSpotMarkets()
    {
        // arrange
        var broker = GetTestBroker();

        // act
        var markets = await broker.GetMarketsAsync(
            new HashSet<string> { "BTC" },
            "USDC",
            assetPermissions: AssetPermissions.LongSpot);

        // assert
        markets.ShouldNotBeNull();
        markets.Values.SelectMany(m => m).Any(m => m.AssetType == AssetType.Spot).ShouldBeTrue();
    }

    [Fact(Skip = "Spot tests failing - invalid data from Hyperliquid")]
    [Trait("Category", "Integration")]
    public async Task GivenShortSpotPermissions_WhenGetMarketsAsync_ShouldIncludeSpotMarkets()
    {
        // arrange
        var broker = GetTestBroker();

        // act
        var markets = await broker.GetMarketsAsync(
            new HashSet<string> { "BTC" },
            "USDC",
            assetPermissions: AssetPermissions.ShortSpot);

        // assert
        markets.ShouldNotBeNull();
        markets.Values.SelectMany(m => m).Any(m => m.AssetType == AssetType.Spot).ShouldBeTrue();
    }

    [Fact(Skip = "Spot tests failing - invalid data from Hyperliquid")]
    [Trait("Category", "Integration")]
    public async Task GivenSpotAndPerpPermissions_WhenGetMarketsAsync_ShouldIncludeBothTypes()
    {
        // arrange
        var broker = GetTestBroker();

        // act
        var markets = await broker.GetMarketsAsync(
            new HashSet<string> { "BTC" },
            "USDC",
            assetPermissions: AssetPermissions.SpotAndPerp);

        // assert
        markets.ShouldNotBeNull();
        var allMarkets = markets.Values.SelectMany(m => m).ToList();
        allMarkets.Any(m => m.AssetType == AssetType.Spot).ShouldBeTrue();
        allMarkets.Any(m => m.AssetType == AssetType.Future).ShouldBeTrue();
    }

    [Fact(Skip = "Spot tests failing - invalid data from Hyperliquid")]
    [Trait("Category", "Integration")]
    public async Task GivenLongSpotAndPerpPermissions_WhenGetMarketsAsync_ShouldIncludeBothTypes()
    {
        // arrange
        var broker = GetTestBroker();

        // act
        var markets = await broker.GetMarketsAsync(
            new HashSet<string> { "BTC" },
            "USDC",
            assetPermissions: AssetPermissions.LongSpotAndPerp);

        // assert
        markets.ShouldNotBeNull();
        var allMarkets = markets.Values.SelectMany(m => m).ToList();
        allMarkets.Any(m => m.AssetType == AssetType.Future).ShouldBeTrue();
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
            assetPermissions: AssetPermissions.PerpetualFutures);

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

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("ETH", 10)]
    [InlineData("BTC", 30)]
    [InlineData("SOL", 365)]
    [InlineData("ETH", 10, true)]
    [InlineData("BTC", 30, true)]
    [InlineData("SOL", 365, true)]
    public async Task GivenValidTicker_WhenGetDailyClosePricesAsync_ShouldReturnPrices(
        string ticker,
        int periods,
        bool includeToday = false)
    {
        // arrange
        var broker = GetTestBroker();

        // act
        var closePrices = await broker.GetDailyClosePricesAsync(ticker, periods, includeToday);

        // assert
        closePrices.ShouldNotBeNull();
        closePrices.Count.ShouldBeGreaterThan(0);
        closePrices.Count.ShouldBeLessThanOrEqualTo(periods);
        closePrices.All(price => price > 0).ShouldBeTrue();

        // Log the prices for verification
        _testOutputHelper.WriteLine($"{ticker} close prices ({closePrices.Count} periods):");
        for (var i = 0; i < closePrices.Count; i++)
        {
            _testOutputHelper.WriteLine($"  Day {i + 1}: {closePrices[i]:N2}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenTickerWithAlias_WhenGetDailyClosePricesAsync_ShouldUseAlias()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            ["SHIB"] = "kSHIB"
        };
        var broker = GetTestBroker(aliases);

        // act
        var closePrices = await broker.GetDailyClosePricesAsync("SHIB", 5);

        // assert
        closePrices.ShouldNotBeNull();
        closePrices.Count.ShouldBeGreaterThan(0);
        closePrices.All(price => price > 0).ShouldBeTrue();

        _testOutputHelper.WriteLine($"SHIB (via kSHIB alias) close prices ({closePrices.Count} periods):");
        for (var i = 0; i < closePrices.Count; i++)
        {
            _testOutputHelper.WriteLine($"  Day {i + 1}: {closePrices[i]:N8}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenInvalidTicker_WhenGetDailyClosePricesAsync_ShouldThrowException()
    {
        // arrange
        var broker = GetTestBroker();

        // act & assert
        await Should.ThrowAsync<Exception>(() => broker.GetDailyClosePricesAsync("INVALIDTICKER123", 10));
    }

    [Fact(Skip = "No bid for BTC/USDC spot")]
    [Trait("Category", "Integration")]
    public async Task GivenMultipleTrades_WhenPlaceTradesAsync_ShouldHandleMixedAssetTypes()
    {
        // arrange
        const string btcUsdc = "BTC/USDC";
        const string eth = "ETH";
        var broker = GetTestBroker();
        var ethFuturesPrice = await GetLimitPrice(broker, eth, AssetType.Future, 0.001);
        var btcSpotPrice = await GetLimitPrice(broker, btcUsdc, AssetType.Spot, 0.0005);

        var trades = new[]
        {
            CreateTrade(eth, AssetType.Future, 0.001, ethFuturesPrice),
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

    private HyperliquidBroker GetTestBroker(
        IReadOnlyDictionary<string, string>? aliases = null,
        bool skipCredentialValidation = false)
    {
        var (address, privateKey) = GetConfig();

        if (!skipCredentialValidation)
        {
            address.ShouldNotBeNullOrEmpty();
            privateKey.ShouldNotBeNullOrEmpty();
        }
        else
        {
            // Use dummy credentials if validation is skipped and credentials are missing
            if (string.IsNullOrEmpty(address))
                address = "0x0000000000000000000000000000000000000000";
            if (string.IsNullOrEmpty(privateKey))
                privateKey = "0000000000000000000000000000000000000000000000000000000000000000";
        }

        var apiCredentials = new HyperLiquidCredentials(address, privateKey);

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
            GetTickerAliasService(aliases),
            address,
            null,
            IsTestnet,
            logger
        );
        return broker;
    }

    private HyperliquidBroker? GetTestBrokerForCleanup()
    {
        var (address, privateKey) = GetConfig();
        if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(privateKey))
            return null;

        return GetTestBroker();
    }

    private async Task CancelOpenOrdersAsync(HyperliquidBroker broker, List<Exception> cleanupErrors)
    {
        var openOrders = await broker.GetOpenOrdersAsync();
        foreach (var order in openOrders.Values)
        {
            try
            {
                _testOutputHelper.WriteLine($"Cancelling open order {order.Id} for {order.Symbol}");
                await broker.CancelOrderAsync(order);
            }
            catch (Exception ex)
            {
                _testOutputHelper.WriteLine($"Failed to cancel order {order.Id} for {order.Symbol}: {ex.Message}");
                cleanupErrors.Add(ex);
            }
        }
    }

    private async Task FlattenFuturesPositionsAsync(HyperliquidBroker broker, List<Exception> cleanupErrors)
    {
        var positions = await broker.GetPositionsAsync();
        var futuresPositions = positions
            .Values
            .SelectMany(x => x)
            .Where(position => position.AssetType == AssetType.Future && position.Amount != 0)
            .ToArray();

        foreach (var position in futuresPositions)
        {
            var flattenTrade = new Trade(
                position.AssetName,
                position.AssetType,
                -position.Amount,
                OrderType: OrderType.Market,
                PostPrice: false,
                ReduceOnly: true,
                ClientOrderId: $"0x{RandomNumberGenerator.GetHexString(32, true)}");

            _testOutputHelper.WriteLine(
                $"Flattening {position.AssetName} futures position {position.Amount} with market order {flattenTrade.Amount}");

            try
            {
                var result = await broker.PlaceTradeAsync(flattenTrade);
                result.Success.ShouldBeTrue(result.Error);
            }
            catch (Exception ex)
            {
                _testOutputHelper.WriteLine($"Failed to flatten {position.AssetName} futures position {position.Amount}: {ex.Message}");
                cleanupErrors.Add(ex);
            }
        }
    }

    private static (string? Address, string? PrivateKey) GetConfig()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        return (config["Hyperliquid:Address"], config["Hyperliquid:PrivateKey"]);
    }

    private async Task<MarketInfo> GetMarketInfo(
        HyperliquidBroker broker,
        string symbol,
        AssetType assetType)
    {
        var assets = symbol.Split('/').Select(s => s.Trim()).ToArray();
        var markets = assetType switch
        {
            AssetType.Spot => await broker.GetMarketsAsync(
                new HashSet<string> { assets[0] },
                assets[1],
                assetPermissions: AssetPermissions.Spot),
            AssetType.Future => await broker.GetMarketsAsync(
                new HashSet<string> { assets[0] },
                assetPermissions: AssetPermissions.PerpetualFutures),
            _ => throw new NotSupportedException($"Asset type {assetType} is not supported.")
        };

        markets.ShouldNotBeNull();
        markets.ShouldContainKey(symbol);
        return markets[symbol][0];
    }

    private decimal GetTestOrderQuantity(MarketInfo marketInfo)
    {
        const decimal minProvideSizeMultiplier = 2m;

        marketInfo.MinProvideSize.ShouldNotBeNull();
        marketInfo.MinProvideSize.Value.ShouldBeGreaterThan(0);

        var rawQuantity = marketInfo.MinProvideSize.Value * minProvideSizeMultiplier;
        var quantityStep = marketInfo.QuantityStep;
        if (quantityStep is not > 0)
        {
            _testOutputHelper.WriteLine(
                $"Calculated test quantity for {marketInfo.Name}: {rawQuantity} (min provide size: {marketInfo.MinProvideSize.Value}, quantity step: null)");
            return rawQuantity;
        }

        var quantity = Math.Ceiling(rawQuantity / quantityStep.Value) * quantityStep.Value;
        _testOutputHelper.WriteLine(
            $"Calculated test quantity for {marketInfo.Name}: {quantity} (min provide size: {marketInfo.MinProvideSize.Value}, quantity step: {quantityStep.Value})");

        return quantity;
    }

    private decimal GetLimitPrice(
        string symbol,
        decimal quantity,
        MarketInfo marketInfo)
    {
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

    private async Task<decimal> GetLimitPrice(
        HyperliquidBroker broker,
        string symbol,
        AssetType assetType,
        double quantity)
    {
        var marketInfo = await GetMarketInfo(broker, symbol, assetType);
        return GetLimitPrice(symbol, Convert.ToDecimal(quantity), marketInfo);
    }
}
