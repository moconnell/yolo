using CryptoExchange.Net.Authentication;
using HyperLiquid.Net.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace YoloBroker.Hyperliquid.Test;

public class HyperliquidBrokerTest
{
    [Theory]
    [InlineData("ETH")]
    [InlineData("BTC", "ETH")]
    public async Task GivenBaseAsset_ShouldGetMarkets(string asset1, string? asset2 = null)
    {
        // arrange
        var baseAssetFilter = new HashSet<string>{asset1};
        if (asset2 != null)
            baseAssetFilter.Add(asset2);
        
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
        Assert.NotEmpty(results);
    }

    private (string? Address, string? PrivateKey) GetConfig()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.local.json", optional: false, reloadOnChange: true)
            .Build();

        return (config["Hyperliquid:Address"], config["Hyperliquid:PrivateKey"]);
    }
}