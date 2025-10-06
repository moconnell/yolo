using RobotWealth.Api.Config;
using YoloAbstractions.Extensions;

using Microsoft.Extensions.Configuration;

namespace RobotWealth.Api.Extensions;

public static class RobotWealthConfigExtensions
{
    public static RobotWealthConfig? GetRobotWealthConfig(this IConfiguration configuration)
    {
        return configuration
            .GetSection(nameof(RobotWealth))
            .Get<RobotWealthConfig>()
            ?.Ensure(c => c.ApiBaseUrl)
            ?.Ensure(c => c.ApiKey)
            ?.Ensure(c => c.VolatilitiesUrlPath)
            ?.Ensure(c => c.WeightsUrlPath);
    }
}