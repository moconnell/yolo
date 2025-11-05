using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RobotWealth.Api;
using RobotWealth.Api.Config;
using RobotWealth.Api.Extensions;
using RobotWealth.Api.Interfaces;
using Unravel.Api;
using Unravel.Api.Config;
using Unravel.Api.Extensions;
using Unravel.Api.Interfaces;
using YoloAbstractions.Config;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;
using YoloWeights;

namespace YoloKonsole.Extensions;

public static class WeightsServiceCollectionExtensions
{
    public static IServiceCollection AddWeightsServices(this IServiceCollection services, IConfiguration config)
    {
        if (config.HasRobotWealthConfig())
        {
            services.AddSingleton<RobotWealthConfig>(_ => config.GetRobotWealthConfig()!);
            services.AddSingleton<IRobotWealthApiService, RobotWealthApiService>();
            services.AddSingleton<IGetFactors, RobotWealthFactorService>();
        }

        if (config.HasUnravelConfig())
        {
            services.AddSingleton<UnravelConfig>(_ => config.GetUnravelConfig()!);
            services.AddSingleton<IUnravelApiService, UnravelApiService>();
            services.AddSingleton<IGetFactors, UnravelFactorService>();
        }

        services.AddHttpClient();
        services.AddSingleton<YoloConfig>(_ => config.GetYoloConfig()!);
        services.AddSingleton<ICalcWeights, YoloWeightsService>();

        return services;
    }
}