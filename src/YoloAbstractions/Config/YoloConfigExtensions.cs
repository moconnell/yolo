using System;
using System.Linq.Expressions;
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

        private static TConfig Ensure<TConfig, TValue>(
            this TConfig config,
            Expression<Func<TConfig, TValue>> selector)
        {
            if (selector.Compile()(config) != null)
            {
                return config;
            }

            var expression = (MemberExpression)selector.Body;
            var memberName = expression.Member.Name;
            
            throw new ConfigException($"Missing or null configuration for {memberName}");
        }
    }
}