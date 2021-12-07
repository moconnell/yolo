// See https://aka.ms/new-console-template for more information
// [Argument(short name (char), long name (string), help text)]

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using YoloAbstractions.Config;
using YoloBroker;
using YoloTrades;
using YoloWeights;

internal class Program
{
    private const int Error = 1;
    private const int Success = 0;
    private static ILogger<Program>? _logger;

    // [Argument('u', "url", "RobotWealth YOLO weights URL")]
    // private static string? Url { get; set; }
    //
    // [Argument('d', "dateFormat", "DateFormat string for dates in response")]
    // private static string? DateFormat { get; set; }

    private static async Task<int> Main(string[] args)
    {
#if DEBUG
        var env = "development"; //Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
#else
        var env = "prod";
#endif

        // Define the cancellation token.
        CancellationTokenSource source = new();
        var cancellationToken = source.Token;

        try
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{env}.json", true, true)
                .AddEnvironmentVariables();

            var config = builder.Build();

            //setup our DI
            var serviceProvider = new ServiceCollection()
                .AddLogging(loggingBuilder => loggingBuilder
                    .AddConsole()
                    .AddFile(config.GetSection("Logging")))
                    // .AddFile("/Users/moc/logs/yolo-{Date}.txt", LogLevel.Debug))
                .AddBroker(config)
                .AddSingleton<ITradeFactory, TradeFactory>()
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            _logger = serviceProvider.GetService<ILoggerFactory>()!
                .CreateLogger<Program>();
            _logger.LogInformation("************ YOLO started ************");

            var yoloConfig = config.GetYoloConfig();

            var weights = (await yoloConfig
                    .GetWeights())
                .ToArray();

            using IYoloBroker broker = serviceProvider.GetService<IYoloBroker>()!;

            var positions = await broker.GetPositionsAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("{Positions}", JsonConvert.SerializeObject(positions));
            }

            var baseAssetFilter = positions
                .Values
                .Select(p => p.BaseAsset)
                .Union(weights.Select(w => w.Ticker.Split("/")
                    .First()))
                .ToHashSet();

            var markets = await broker.GetMarketsAsync(
                baseAssetFilter,
                yoloConfig.BaseAsset,
                yoloConfig.AssetPermissions,
                cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("{Markets}", JsonConvert.SerializeObject(markets));
            }

            var tradeFactory = serviceProvider.GetService<ITradeFactory>()!;

            var trades = tradeFactory
                .CalculateTrades(weights, positions, markets)
                .ToArray();

            if (!trades.Any())
            {
                return Success;
            }

            Console.WriteLine();
            Console.Write("Proceed? (y/n): ");

            if (Console.Read() != 'y')
            {
                return Success;
            }

            await foreach (var result in broker.PlaceTradesAsync(trades, cancellationToken))
            {
                if (result.Success)
                {
                    _logger.PlacedOrder(result.Order!.AssetName, result.Order);
                }
                else
                {
                    _logger.OrderError(result.Trade.AssetName, result.Error, result.ErrorCode);
                }
            }

            return Success;
        }
        catch (Exception e)
        {
            source.Cancel();

            _logger?.LogError("Error occurred: {Error}", e);

            return Error;
        }
        finally
        {
            _logger?.LogInformation("************ YOLO ended ************");
        }
    }
}