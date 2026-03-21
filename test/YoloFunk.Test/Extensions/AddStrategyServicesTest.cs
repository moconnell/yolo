using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YoloBroker.Interface;
using YoloFunk.Extensions;

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
}
