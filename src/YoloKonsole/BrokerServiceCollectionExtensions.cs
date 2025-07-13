using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YoloAbstractions.Config;
using YoloBroker.Hyperliquid;
using YoloBroker.Hyperliquid.Config;
using YoloBroker.Interface;

namespace YoloKonsole;

public static class BrokerServiceCollectionExtensions
{
    public static IServiceCollection AddBroker(
        this IServiceCollection services,
        IConfiguration config)
    {
        if (config.HasHyperliquidConfig())
        {
            return services.AddSingleton<IYoloBroker, HyperliquidBroker>();
        }

        throw new ConfigException("No broker configuration!");
    }
}