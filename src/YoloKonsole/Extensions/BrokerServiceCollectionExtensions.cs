using CryptoExchange.Net.Authentication;
using HyperLiquid.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YoloAbstractions.Exceptions;
using YoloAbstractions.Interfaces;
using YoloBroker;
using YoloBroker.Hyperliquid;
using YoloBroker.Hyperliquid.Config;
using YoloBroker.Interface;

namespace YoloKonsole.Extensions;

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
            
            services.AddSingleton<ITickerAliasService>(new TickerAliasService(hyperliquidConfig.Aliases));
            services.AddSingleton<IYoloBroker, HyperliquidBroker>();
            services.AddSingleton<IGetFactors, BrokerVolatilityFactorService>();

            return services;
        }

        throw new ConfigException("No broker configuration!");
    }
}