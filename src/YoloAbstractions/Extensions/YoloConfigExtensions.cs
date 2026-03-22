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
            ?.Ensure(c => c.AssetPermissions > AssetPermissions.None)
            ?.Ensure(c => c.BaseAsset)
            ?.Ensure(c => c.MaxLeverage > 0)
            ?.Ensure(c => c.MaxRepriceRetries >= 0)
            ?.Ensure(c => c.MaxWeightingAbs > 0)
            ?.Ensure(c => c.MinOrderValue == null || c.MinOrderValue > 0)
            ?.Ensure(c => c.NominalCash > 0)
            ?.Ensure(c => c.SpreadSplit >= 0 && c.SpreadSplit <= 1)
            ?.Ensure(c => c.TradeBuffer >= 0)
            ?.Ensure(c => TimeSpan.Parse(c.UnfilledOrderTimeout) > TimeSpan.Zero);
    }
}
