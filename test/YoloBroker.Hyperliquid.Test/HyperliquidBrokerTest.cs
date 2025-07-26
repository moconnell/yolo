using System.Security.Cryptography;
using CryptoExchange.Net.Authentication;
using HyperLiquid.Net;
using HyperLiquid.Net.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using YoloAbstractions;

namespace YoloBroker.Hyperliquid.Test;

public class HyperliquidBrokerTest
{
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("ETH", AssetPermissions.PerpetualFutures, "ETH")]
    [InlineData("BTC,ETH", AssetPermissions.PerpetualFutures, "BTC,ETH")]
    public async Task GivenBaseAsset_ShouldGetMarkets(string baseAssetFilterString, AssetPermissions assetPermissions, string expectedMarketsString)
    {
        // arrange
        var baseAssetFilter = baseAssetFilterString.Split(',').Select(asset => asset.Trim()).ToHashSet();
        var expectedMarkets = expectedMarketsString.Split(',').Select(market => market.Trim()).ToHashSet();

        HyperliquidBroker broker = GetTestBroker();

        // act
        var results = await broker.GetMarketsAsync(baseAssetFilter, assetPermissions: assetPermissions);

        // assert
        Assert.NotNull(results);
        Assert.Equal(expectedMarkets.Count, results.Count);
        Assert.True(results.All(kvp => expectedMarkets.Contains(kvp.Value[0].Name)));
        Assert.True(results.All(kvp => kvp.Value.All(info => info.Ask > 0)));
        Assert.True(results.All(kvp => kvp.Value.All(info => info.Bid > 0)));
        Assert.True(results.All(kvp => kvp.Value.All(info => info.Mid > 0)));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ShouldGetOpenOrders()
    {
        // arrange
        HyperliquidBroker broker = GetTestBroker();

        // act
        var results = await broker.GetOrdersAsync();

        // assert
        Assert.NotNull(results);
    }

    [Theory]
    [Trait("Category", "Integration")]
    // [InlineData("HYPE/USDC", AssetType.Spot, 1)]
    [InlineData("ETH", AssetType.Future, 0.01)]
    [InlineData("BTC", AssetType.Future, 0.01)]
    public async Task ShouldPlaceOrder(string symbol, AssetType assetType, double quantity, bool isLimitOrder = true)
    {
        // arrange
        HyperliquidBroker broker = GetTestBroker();
        decimal? orderPrice = isLimitOrder ? await GetLimitPrice() : null;
        var trade = CreateTrade(symbol, assetType, quantity, orderPrice);

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
            order.AssetName.ShouldBe(symbol);
            order.Amount.ShouldBe(trade.Amount);

            await broker.CancelOrderAsync(order.AssetName, order.Id);
        }

        async Task<decimal> GetLimitPrice()
        {
            // Get current market price first
            var assets = symbol.Split('/').Select(s => s.Trim()).ToArray();
            var markets = assetType switch
            {
                AssetType.Spot => await broker.GetMarketsAsync(new HashSet<string> { assets[0] }, assets[1], assetPermissions: AssetPermissions.Spot),
                AssetType.Future => await broker.GetMarketsAsync(new HashSet<string> { assets[0] }, assetPermissions: AssetPermissions.PerpetualFutures),
                _ => throw new NotSupportedException($"Asset type {assetType} is not supported.")
            };

            markets.ShouldNotBeNull();
            markets.ShouldContainKey(symbol);
            var currentPrice = quantity < 0 ? markets[symbol][0].Ask : markets[symbol][0].Bid;
            currentPrice.ShouldNotBeNull();
            currentPrice.Value.ShouldBeGreaterThan(0);

            return currentPrice.Value;
        }

        static Trade CreateTrade(string symbol, AssetType assetType, double quantity, decimal? price) =>
            new(symbol, assetType, Convert.ToDecimal(quantity), price, ClientOrderId: $"0x{RandomNumberGenerator.GetHexString(32, true)}");
    }

    private static HyperliquidBroker GetTestBroker()
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
            mockLogger.Object
        );
        return broker;
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