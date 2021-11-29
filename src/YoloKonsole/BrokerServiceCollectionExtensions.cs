using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YoloAbstractions.Config;
using YoloBroker;
using YoloBroker.Binance;
using YoloBroker.Ftx;
using YoloBroker.Ftx.Config;

public static class BrokerServiceCollectionExtensions 
{
    public static IServiceCollection AddBroker(this IServiceCollection services, IConfiguration config)
    {
        if (config.HasBinanceConfig())
        {
            return services.AddSingleton<IYoloBroker, BinanceBroker>(); 
        }

        if (config.HasFtxConfig())
        {
            return services.AddSingleton<IYoloBroker, FtxBroker>(); 
        }

        throw new ConfigException("No broker configuration!");
    }
}