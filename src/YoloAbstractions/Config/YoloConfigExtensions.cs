using Microsoft.Extensions.Configuration;
using static YoloAbstractions.Config.WellKnown.ConfigSections;

namespace YoloAbstractions.Config;

public static class YoloConfigExtensions
{
    public static YoloConfig? GetYoloConfig(this IConfiguration configuration)
    {
        return configuration
            .GetSection(Yolo)
            .Get<YoloConfig>()
            ?.Ensure(c => c.ApiBaseUrl)
            ?.Ensure(c => c.ApiKey)
            ?.Ensure(c => c.BaseAsset)
            ?.Ensure(c => c.VolatilitiesUrlPath)
            ?.Ensure(c => c.WeightsUrlPath)
            ?.Ensure(c => c.TradeBuffer);
    }
}