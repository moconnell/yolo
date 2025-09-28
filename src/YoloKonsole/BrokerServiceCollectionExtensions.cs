using CryptoExchange.Net.Authentication;
using HyperLiquid.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YoloAbstractions.Exceptions;
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
            var hyperliquidConfig = config.GetHyperliquidConfig()!;
            services.AddHyperLiquid(options =>
            {
                options.ApiCredentials = new ApiCredentials(hyperliquidConfig.Address, hyperliquidConfig.PrivateKey);

                if (hyperliquidConfig.UseTestnet)
                {
                    options.Environment = HyperLiquidEnvironment.Testnet;
                    options.Rest.OutputOriginalData = true;
                    options.Socket.OutputOriginalData = true;
                }
            });

            return services.AddSingleton<IYoloBroker, HyperliquidBroker>();
        }

        throw new ConfigException("No broker configuration!");
    }
}