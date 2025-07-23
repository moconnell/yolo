using CryptoExchange.Net.Authentication;
using HyperLiquid.Net.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace YoloBroker.Hyperliquid.Test;

public class HyperliquidBrokerTest
{
    [Theory]
    [InlineData("ETH", "ETH")]
    [InlineData("BTC,ETH", "BTC,ETH")]
    public async Task GivenBaseAsset_ShouldGetMarkets(string baseAssetFilterString, string expectedMarketsString)
    {
        // arrange
        var baseAssetFilter = baseAssetFilterString.Split(',').Select(asset => asset.Trim()).ToHashSet();
        var expectedMarkets = expectedMarketsString.Split(',').Select(market => market.Trim()).ToHashSet();
        var (address, privateKey) = GetConfig();
        Assert.NotNull(address);
        Assert.NotNull(privateKey);
        var apiCredentials = new ApiCredentials(address, privateKey);
        var mockLogger = new Mock<ILogger<HyperliquidBroker>>();

        var broker = new HyperliquidBroker(
            new HyperLiquidRestClient(options => options.ApiCredentials = apiCredentials),
            new HyperLiquidSocketClient(options => options.ApiCredentials = apiCredentials),
            mockLogger.Object
        );

        // act
        var results = await broker.GetMarketsAsync(baseAssetFilter);

        // assert
        Assert.NotNull(results);
        Assert.Equal(expectedMarkets.Count, results.Count);
        Assert.True(results.All(kvp => expectedMarkets.Contains(kvp.Value[0].Name)));
        Assert.True(results.All(kvp => kvp.Value.All(info => info.Ask > 0)));
        Assert.True(results.All(kvp => kvp.Value.All(info => info.Bid > 0)));
        Assert.True(results.All(kvp => kvp.Value.All(info => info.Mid > 0)));
    }

    [Fact]
    public async Task ShouldGetOpenOrders()
    {
        // arrange
        var (address, privateKey) = GetConfig();
        Assert.NotNull(address);
        Assert.NotNull(privateKey);
        var apiCredentials = new ApiCredentials(address, privateKey);
        var mockLogger = new Mock<ILogger<HyperliquidBroker>>();

        var broker = new HyperliquidBroker(
            new HyperLiquidRestClient(options => options.ApiCredentials = apiCredentials),
            new HyperLiquidSocketClient(options => options.ApiCredentials = apiCredentials),
            mockLogger.Object
        );

        // act
        var results = await broker.GetOrdersAsync();

        // assert
        Assert.NotNull(results);
    }

    private static (string? Address, string? PrivateKey) GetConfig()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.local.json", optional: false, reloadOnChange: true)
            .Build();

        return (config["Hyperliquid:Address"], config["Hyperliquid:PrivateKey"]);
    }
}