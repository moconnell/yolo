using Microsoft.Extensions.Configuration;
using YoloAbstractions.Config;
using static YoloBroker.Hyperliquid.Config.WellKnown;

namespace YoloBroker.Hyperliquid.Config;

public static class HyperliquidConfigExtensions
{
    public static bool HasHyperliquidConfig(this IConfiguration configuration)
    {
        try
        {
            return configuration.GetHyperliquidConfig() is not null;
        }
        catch
        {
            return false;
        }
    }

    public static HyperliquidConfig? GetHyperliquidConfig(this IConfiguration configuration)
    {
        return configuration
            .GetSection(ConfigSections.Hyperliquid)
            .Get<HyperliquidConfig>()
            .Ensure(c => c != null)
            ?.Ensure(c => !string.IsNullOrEmpty(c.Address))
            ?.Ensure(c => !string.IsNullOrEmpty(c.PrivateKey));
    }
}