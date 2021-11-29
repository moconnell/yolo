using Microsoft.Extensions.Configuration;
using YoloAbstractions.Config;
using static YoloBroker.Binance.Config.WellKnown;

namespace YoloBroker.Binance
{
    public static class BinanceConfigExtensions
    {
        public static bool HasBinanceConfig(this IConfiguration configuration)
        {
            return configuration
                .GetSection(ConfigSections.Binance)
                .Get<BinanceConfig>() is { };
        }

        public static BinanceConfig GetBinanceConfig(this IConfiguration configuration)
        {
            return configuration
                .GetSection(ConfigSections.Binance)
                .Get<BinanceConfig>()
                .Ensure(c => c.ApiKey)
                .Ensure(c => c.BaseAddress)
                .Ensure(c => c.BaseAddressCoinFutures)
                .Ensure(c => c.BaseAddressSocketClient)
                .Ensure(c => c.BaseAddressUsdtFutures)
                .Ensure(c => c.Secret);
        }
    }
}