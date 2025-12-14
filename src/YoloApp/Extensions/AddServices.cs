using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YoloAbstractions.Interfaces;
using YoloTrades;

namespace YoloApp.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddWeightsServices(config)
                .AddCommands()
                .AddConfig(config)
                .AddSingleton<ITradeFactory, TradeFactory>();

        return services;
    }
}