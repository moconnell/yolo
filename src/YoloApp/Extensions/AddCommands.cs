using Microsoft.Extensions.DependencyInjection;
using YoloAbstractions.Interfaces;

namespace YoloApp.Extensions;

public static partial class ServiceCollectionExtensions
{
    internal static IServiceCollection AddCommands(this IServiceCollection services)
    {
        services.AddKeyedTransient<ICommand, Commands.RebalanceCommand>("rebalance");

        return services;
    }
}