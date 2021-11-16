using System;
using Microsoft.Extensions.Configuration;
using YoloAbstractions.Config;
using static YoloBroker.Binance.Config.WellKnown;

namespace YoloBroker.Binance
{
    public static class BinanceConfigExtensions
    {
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

        private static TConfig Ensure<TConfig, TValue>(
            this TConfig config,
            Func<TConfig, TValue> selector)
        {
            if (selector(config) == null)
                throw new ConfigException("Missing or null configuration");

            return config;
        }
    }
}