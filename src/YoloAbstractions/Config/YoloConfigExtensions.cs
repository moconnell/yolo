using Microsoft.Extensions.Configuration;
using static YoloAbstractions.Config.WellKnown.ConfigSections;

namespace YoloAbstractions.Config
{
    public static class YoloConfigExtensions
    {
        public static YoloConfig GetYoloConfig(this IConfiguration configuration)
        {
            return configuration
                .GetSection(Yolo)
                .Get<YoloConfig>()
                .Ensure(c => c.BaseCurrencyToken)
                .Ensure(c => c.WeightsUrl)
                .Ensure(c => c.TradeBuffer);
        }
    }
}