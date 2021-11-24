using Microsoft.Extensions.Configuration;
using YoloAbstractions.Config;
using static YoloBroker.Ftx.Config.WellKnown;

namespace YoloBroker.Ftx.Config
{
    public static class FtxConfigExtensions
    {
        public static bool HasFtxConfig(this IConfiguration configuration)
        {
            return configuration
                .GetSection(ConfigSections.Ftx)
                .Get<FtxConfig>() is { };
        }
        
        public static FtxConfig GetFtxConfig(this IConfiguration configuration)
        {
            return configuration
                .GetSection(ConfigSections.Ftx)
                .Get<FtxConfig>()
                .Ensure(c => c.ApiKey)
                .Ensure(c => c.BaseAddress)
                .Ensure(c => c.Secret);
        }
    }
}