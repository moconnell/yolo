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
using static YoloAbstractions.AssetPermissions;
using static YoloAbstractions.AssetType;
using static YoloAbstractions.OrderType;

namespace YoloBroker.Hyperliquid.Test;

public class HyperliquidBrokerTest(ITestOutputHelper testOutputHelper)
{
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
                testOutputHelper.WriteLine($"Update {updateCount}: {orderUpdate.Type} - {orderUpdate.Symbol}");

                orderUpdate.ShouldNotBeNull();

                if (orderUpdate.Type == OrderUpdateType.Error)
                {
                    testOutputHelper.WriteLine($"Error: {orderUpdate.Error?.Message}");
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
            testOutputHelper.WriteLine($"Test completed with {updateCount} updates");
        }
        finally
        {
            // clean-up
            foreach (var order in orders.Values)
            {
                await broker.CancelOrderAsync(order);
            }
        }
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

        testOutputHelper.WriteLine(
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


    private static HyperliquidBroker GetTestBroker(IReadOnlyDictionary<string, string>? aliases = null)
    {
        var (address, privateKey) = GetConfig();
        address.ShouldNotBeNullOrEmpty();
        privateKey.ShouldNotBeNullOrEmpty();

        var apiCredentials = new ApiCredentials(address, privateKey);

        var mockLogger = new Mock<ILogger<HyperliquidBroker>>();

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
            mockLogger.Object
        );
        return broker;

        IGetTickerAlias GetTickerAliasService()
        {
            if (aliases != null)
            {
                return new TickerAliasService(aliases);
            }

            var mockTickerAliasService = new Mock<IGetTickerAlias>();
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
}