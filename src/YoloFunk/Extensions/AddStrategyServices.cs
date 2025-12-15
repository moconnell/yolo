using CryptoExchange.Net.Authentication;
using HyperLiquid.Net;
using HyperLiquid.Net.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RobotWealth.Api;
using RobotWealth.Api.Config;
using Unravel.Api;
using Unravel.Api.Config;
using YoloAbstractions.Config;
using YoloAbstractions.Exceptions;
using YoloAbstractions.Interfaces;
using YoloApp.Commands;
using YoloBroker;
using YoloBroker.Hyperliquid;
using YoloBroker.Hyperliquid.Config;
using YoloBroker.Interface;
using YoloWeights;

namespace YoloFunk.Extensions;

public static class AddStrategyServices
{
    public static IServiceCollection AddStrategy(
        this IServiceCollection services,
        IConfiguration config,
        string strategyKey,
        string configSection)
    {
        var strategyConfig = config.GetSection(configSection);

        // Register YoloConfig for this strategy
        var yoloConfig = strategyConfig.GetSection("Yolo").Get<YoloConfig>();
        if (yoloConfig is null)
        {
            throw new InvalidOperationException($"Strategy '{strategyKey}' missing Yolo configuration in section '{configSection}'");
        }

        services.AddKeyedSingleton(strategyKey, yoloConfig);

        // Register broker for this strategy
        var hyperliquidConfig = strategyConfig.GetSection("Hyperliquid").Get<HyperliquidConfig>();
        if (hyperliquidConfig is null)
        {
            throw new ConfigException($"Strategy '{strategyKey}' missing Hyperliquid broker configuration");
        }

        services.AddKeyedSingleton<IYoloBroker>(strategyKey, (sp, key) =>
        {
            // Create strategy-specific HyperLiquid clients
            var restClient = new HyperLiquidRestClient(options =>
            {
                options.ApiCredentials = new ApiCredentials(hyperliquidConfig.Address, hyperliquidConfig.PrivateKey);

                if (hyperliquidConfig.UseTestnet)
                {
                    options.Environment = HyperLiquidEnvironment.Testnet;
                    options.OutputOriginalData = true;
                }
            });

            var socketClient = new HyperLiquidSocketClient(options =>
            {
                options.ApiCredentials = new ApiCredentials(hyperliquidConfig.Address, hyperliquidConfig.PrivateKey);

                if (hyperliquidConfig.UseTestnet)
                {
                    options.Environment = HyperLiquidEnvironment.Testnet;
                    options.OutputOriginalData = true;
                }
            });

            var tickerAliasService = new TickerAliasService(hyperliquidConfig.Aliases);

            return new HyperliquidBroker(
                restClient,
                socketClient,
                tickerAliasService,
                hyperliquidConfig,
                sp.GetRequiredService<ILogger<HyperliquidBroker>>());
        });

        // Register factor providers for this strategy
        var factorProviders = new List<string>();

        var robotWealthConfig = strategyConfig.GetSection("RobotWealth").Get<RobotWealthConfig>();
        if (robotWealthConfig is not null)
        {
            var rwKey = $"{strategyKey}-robotwealth";
            services.AddKeyedSingleton(rwKey, robotWealthConfig);
            services.AddKeyedSingleton<IGetFactors>(rwKey, (sp, key) =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                var apiService = new RobotWealthApiService(httpClient, robotWealthConfig);
                return new RobotWealthFactorService(apiService);
            });
            factorProviders.Add(rwKey);
        }

        var unravelConfig = strategyConfig.GetSection("Unravel").Get<UnravelConfig>();
        if (unravelConfig is not null)
        {
            var bvKey = $"{strategyKey}-broker-volatility";
            services.AddKeyedSingleton<IGetFactors>(bvKey, (sp, key) =>
            {
                return new BrokerVolatilityFactorService(
                    sp.GetRequiredKeyedService<IYoloBroker>(strategyKey));
            });
            factorProviders.Add(bvKey);

            var unKey = $"{strategyKey}-unravel";
            services.AddKeyedSingleton(unKey, unravelConfig);
            services.AddKeyedSingleton<IGetFactors>(unKey, (sp, key) =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                var apiService = new UnravelApiService(httpClient, unravelConfig);
                return new UnravelFactorService(apiService);
            });
            factorProviders.Add(unKey);
        }

        // Register ICalcWeights for this strategy
        services.AddKeyedSingleton<ICalcWeights>(strategyKey, (sp, key) =>
        {
            var factors = factorProviders
                .Select(fpKey => sp.GetRequiredKeyedService<IGetFactors>(fpKey))
                .ToArray();

            return new YoloWeightsService(
                factors,
                sp.GetRequiredKeyedService<YoloConfig>(strategyKey),
                sp.GetRequiredService<ILogger<YoloWeightsService>>());
        });

        // Register RebalanceCommand for this strategy
        services.AddKeyedScoped<ICommand>(strategyKey, (sp, key) =>
        {
            var yoloConfigOptions = Options.Create(sp.GetRequiredKeyedService<YoloConfig>(strategyKey));

            return new RebalanceCommand(
                sp.GetRequiredKeyedService<ICalcWeights>(strategyKey),
                sp.GetRequiredService<ITradeFactory>(),
                sp.GetRequiredKeyedService<IYoloBroker>(strategyKey),
                yoloConfigOptions,
                sp.GetRequiredService<ILogger<RebalanceCommand>>());
        });

        return services;
    }
}
