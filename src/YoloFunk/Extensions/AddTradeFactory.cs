using Microsoft.Extensions.DependencyInjection;
using YoloAbstractions.Interfaces;
using YoloTrades;

namespace YoloFunk.Extensions;

public static partial class ServiceCollectionExtensions
{
    internal static IServiceCollection AddTradeFactory(this IServiceCollection services)
    {
        services.AddSingleton<ITradeFactory, TradeFactory>();
        return services;
    }
}
