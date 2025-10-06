using Microsoft.Extensions.Configuration;
using YoloAbstractions.Config;
using static YoloAbstractions.Config.WellKnown.ConfigSections;

namespace YoloAbstractions.Extensions;

public static class YoloConfigExtensions
{
    public static YoloConfig? GetYoloConfig(this IConfiguration configuration)
    {
        return configuration
            .GetSection(Yolo)
            .Get<YoloConfig>()
            ?.Ensure(c => c.BaseAsset)
            ?.Ensure(c => c.TradeBuffer);
    }
}
