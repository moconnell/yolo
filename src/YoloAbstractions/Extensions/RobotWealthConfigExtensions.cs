using Microsoft.Extensions.Configuration;
using static YoloAbstractions.Config.WellKnown.ConfigSections;

namespace YoloAbstractions.Config;

public static class RobotWealthConfigExtensions
{
    public static RobotWealthConfig? GetRobotWealthConfig(this IConfiguration configuration)
    {
        return configuration
            .GetSection(RobotWealth)
            .Get<RobotWealthConfig>()
            ?.Ensure(c => c.ApiBaseUrl)
            ?.Ensure(c => c.ApiKey)
            ?.Ensure(c => c.VolatilitiesUrlPath)
            ?.Ensure(c => c.WeightsUrlPath);
    }
}