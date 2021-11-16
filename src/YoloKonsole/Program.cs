// See https://aka.ms/new-console-template for more information
// [Argument(short name (char), long name (string), help text)]

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using YoloAbstractions.Config;
using YoloBroker;
using YoloBroker.Binance;
using YoloTrades;
using YoloWeights;

internal static class Program
{
    // [Argument('u', "url", "RobotWealth YOLO weights URL")]
    // private static string? Url { get; set; }
    //
    // [Argument('d', "dateFormat", "DateFormat string for dates in response")]
    // private static string? DateFormat { get; set; }

    private static async Task Main(string[] args)
    {
#if DEBUG
        var env = "development"; //Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
#else
        var env = "prod";
#endif

        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", true, true)
            .AddJsonFile($"appsettings.{env}.json", true, true)
            .AddEnvironmentVariables();

        var config = builder.Build();

        var yoloConfig = config.GetYoloConfig();

        var weights = await yoloConfig
            .GetWeights();

        var binanceConfig = config.GetBinanceConfig();
        using var broker = new BinanceBroker(binanceConfig);
        var positions = await broker.GetPositionsAsync();
        var prices = await broker.GetPricesAsync();
        var symbols = await broker.GetSymbolsAsync();

        var trades = weights
            .CalculateTrades(
                positions,
                prices,
                symbols,
                yoloConfig.TradeBuffer,
                yoloConfig.MaxLeverage,
                yoloConfig.NominalCash,
                yoloConfig.BaseCurrencyToken,
                yoloConfig.TradePreference)
            .ToArray();

        Console.WriteLine("Generated trades:");
        Console.WriteLine();

        foreach (var trade in trades)
        {
            Console.WriteLine(trade);
        }

        Console.WriteLine();
        Console.Write("Proceed? (y/n): ");

        if (Console.Read() == 'y')
        {
            try
            {
                await broker.PlaceTradesAsync(trades);
            }
            catch (BrokerException e)
            {
                Console.WriteLine();
                Console.Write(e.Message);
            }
        }
    }
}