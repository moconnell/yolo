using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YoloAbstractions.Exceptions;
using YoloAbstractions.Interfaces;
using YoloApp.Commands;
using YoloBroker.Interface;
using YoloFunk.Extensions;
using YoloFunk.Infrastructure;

namespace YoloFunk.Test.Extensions;

public class AddStrategyServicesTest
{
    [Fact]
    public void AddStrategy_WhenYoloConfigMissing_ShouldThrow()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Strategies:Test:Hyperliquid:Address"] = "0xabc",
                ["Strategies:Test:Hyperliquid:PrivateKey"] = "key",
                ["Strategies:Test:Hyperliquid:UseTestnet"] = "true"
            })
            .Build();

        var services = new ServiceCollection();

        Should.Throw<InvalidOperationException>(() =>
            services.AddStrategy(config, "test", "Strategies:Test"));
    }

    [Fact]
    public void AddStrategy_WhenValidConfig_ShouldRegisterKeyedOrderManager()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Strategies:Test:Yolo:BaseAsset"] = "USDC",
                ["Strategies:Test:Hyperliquid:Address"] = "0xabc",
                ["Strategies:Test:Hyperliquid:PrivateKey"] = "key",
                ["Strategies:Test:Hyperliquid:UseTestnet"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddStrategy(config, "test", "Strategies:Test");

        using var provider = services.BuildServiceProvider();
        var orderManager = provider.GetRequiredKeyedService<IOrderManager>("test");

        orderManager.ShouldNotBeNull();
    }

    [Fact]
    public void AddStrategy_WhenHyperliquidConfigMissing_ShouldThrow()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Strategies:Test:Yolo:BaseAsset"] = "USDC"
            })
            .Build();

        var services = new ServiceCollection();

        Should.Throw<ConfigException>(() =>
            services.AddStrategy(config, "test", "Strategies:Test"));
    }

    [Fact]
    public void AddStrategy_WhenRobotWealthAndUnravelConfigured_ShouldRegisterStrategyServices()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Strategies:Test:Yolo:BaseAsset"] = "USDC",
                ["Strategies:Test:Yolo:TradeBuffer"] = "0.01",
                ["Strategies:Test:Hyperliquid:Address"] = "0x1111111111111111111111111111111111111111",
                ["Strategies:Test:Hyperliquid:PrivateKey"] = "key",
                ["Strategies:Test:Hyperliquid:UseTestnet"] = "true",
                ["Strategies:Test:RobotWealth:ApiBaseUrl"] = "https://example.com",
                ["Strategies:Test:RobotWealth:ApiKey"] = "rw-key",
                ["Strategies:Test:Unravel:ApiBaseUrl"] = "https://example.com",
                ["Strategies:Test:Unravel:ApiKey"] = "un-key",
                ["Strategies:Test:Unravel:UseLiveFactors"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();

        services.AddStrategy(config, "test", "Strategies:Test");

        using var provider = services.BuildServiceProvider();
        var weights = provider.GetRequiredKeyedService<ICalcWeights>("test");
        var command = provider.GetRequiredKeyedService<ICommand>("test");

        weights.ShouldNotBeNull();
        command.ShouldNotBeNull();
    }

    [Fact]
    public void AddStrategy_WhenAzureStorageConfigured_ShouldRegisterTradeIngestionServices()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureWebJobsStorage"] = "UseDevelopmentStorage=true",
                ["Strategies:Test:Yolo:BaseAsset"] = "USDC",
                ["Strategies:Test:Hyperliquid:Address"] = "0x1111111111111111111111111111111111111111",
                ["Strategies:Test:Hyperliquid:PrivateKey"] = "key",
                ["Strategies:Test:Hyperliquid:UseTestnet"] = "true",
                ["Strategies:Test:TradeIngestion:WindowDays"] = "1",
                ["Strategies:Test:TradeIngestion:OverlapDays"] = "2"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new Mock<TableServiceClient>().Object);

        services.AddStrategy(config, "test", "Strategies:Test");

        using var provider = services.BuildServiceProvider();
        var tradeSource = provider.GetRequiredKeyedService<IUserTradeSource>("test");
        var ingestionService = provider.GetRequiredKeyedService<IUserTradeIngestionService>("test");

        tradeSource.ShouldNotBeNull();
        ingestionService.ShouldNotBeNull();
    }
}
