using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YoloAbstractions.Config;

namespace YoloApp.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfig(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(config);
        services.Configure<YoloConfig>(config.GetRequiredSection("YoloConfig"));

        return services;
    }
}