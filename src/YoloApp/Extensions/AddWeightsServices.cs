using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RobotWealth.Api;
using RobotWealth.Api.Extensions;
using RobotWealth.Api.Interfaces;
using Unravel.Api;
using Unravel.Api.Extensions;
using Unravel.Api.Interfaces;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;
using YoloWeights;

namespace YoloApp.Extensions;

public static partial class ServiceCollectionExtensions
{
    internal static IServiceCollection AddWeightsServices(this IServiceCollection services, IConfiguration config)
    {
        if (config.HasRobotWealthConfig())
        {
            services.AddSingleton(_ => config.GetRobotWealthConfig()!);
            services.AddSingleton<IRobotWealthApiService, RobotWealthApiService>();
            services.AddSingleton<IGetFactors, RobotWealthFactorService>();
        }

        if (config.HasUnravelConfig())
        {
            services.AddSingleton(_ => config.GetUnravelConfig()!);
            services.AddSingleton<IUnravelApiService, UnravelApiService>();
            services.AddSingleton<IGetFactors, UnravelFactorService>();
        }

        services.AddHttpClient();
        services.AddSingleton(_ => config.GetYoloConfig()!);
        services.AddSingleton<ICalcWeights, YoloWeightsService>();

        return services;
    }
}