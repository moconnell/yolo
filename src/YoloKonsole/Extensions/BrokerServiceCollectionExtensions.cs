using CryptoExchange.Net.Authentication;
using HyperLiquid.Net;
using HyperLiquid.Net.Interfaces.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.Util;
using YoloAbstractions.Exceptions;
using YoloAbstractions.Interfaces;
using YoloBroker;
using YoloBroker.AzureVault.Extensions;
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
                options.ApiCredentials = new ApiCredentials(hyperliquidConfig.Address, hyperliquidConfig.PrivateKey.IsValidEthereumAddressHexFormat() ? hyperliquidConfig.PrivateKey : "0x0");

                if (hyperliquidConfig.UseTestnet)
                {
                    options.Environment = HyperLiquidEnvironment.Testnet;
                    options.Rest.OutputOriginalData = true;
                    options.Socket.OutputOriginalData = true;
                }
            });

            services.AddSingleton<ITickerAliasService>(new TickerAliasService(hyperliquidConfig.Aliases));
            services.AddSingleton<IYoloBroker>(serviceProvider =>
            {
                HyperliquidBroker hyperliquidBroker = new(
                    serviceProvider.GetRequiredService<IHyperLiquidRestClient>(),
                    serviceProvider.GetRequiredService<IHyperLiquidSocketClient>(),
                    serviceProvider.GetRequiredService<ITickerAliasService>(),
                    serviceProvider.GetRequiredService<ILogger<HyperliquidBroker>>()
                );
                hyperliquidBroker.ConfigureAzureKeyVaultSigner(config);
                return hyperliquidBroker;
            });
            services.AddSingleton<IGetFactors, BrokerVolatilityFactorService>();

            return services;
        }

        throw new ConfigException("No broker configuration!");
    }
}