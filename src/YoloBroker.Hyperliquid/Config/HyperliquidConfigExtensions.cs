using Microsoft.Extensions.Configuration;
using YoloAbstractions.Config;
using static YoloBroker.Hyperliquid.Config.WellKnown;

namespace YoloBroker.Hyperliquid.Config;

public static class HyperliquidConfigExtensions
{
    public static bool HasHyperliquidConfig(this IConfiguration configuration) =>
        configuration
            .GetSection(ConfigSections.Hyperliquid)
            .Get<HyperliquidConfig>() is { };

    public static HyperliquidConfig? GetHyperliquidConfig(this IConfiguration configuration)
    {
        return configuration
            .GetSection(ConfigSections.Hyperliquid)
            .Get<HyperliquidConfig>()
            .Ensure(c => c != null)
            .Ensure(c => c!.Address)
            .Ensure(c => c!.PrivateKey);
    }
}