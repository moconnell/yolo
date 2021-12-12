using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Utility.CommandLine;
using YoloAbstractions.Config;
using YoloBroker;
using YoloTrades;
using YoloWeights;

namespace YoloKonsole;

internal class Program
{
    private const int Error = 1;
    private const int Success = 0;
    private static ILogger<Program>? _logger;

    [Argument('s', "silent", "Silent running: no challenge will be issued before placing trades")]
    internal static bool Silent { get; set; }

    private static async Task<int> Main(string[] args)
    {
#if DEBUG
        var env = "development"; //Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
#else
        var env = "prod";
#endif

        CancellationTokenSource source = new();
        var cancellationToken = source.Token;

        try
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{env}.json", true, true)
                .AddEnvironmentVariables();

            var config = builder.Build();

            var serviceProvider = new ServiceCollection()
                .AddLogging(loggingBuilder => loggingBuilder
                    .AddConsole()
                    .AddFile(config.GetSection("Logging")))
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

            using var broker = serviceProvider.GetService<IYoloBroker>()!;

            var positions = await broker.GetPositionsAsync(cancellationToken);

            var baseAssetFilter = positions
                .Values
                .Select(p => p.BaseAsset)
                .Union(weights.Select(w => w.Ticker.Split("/")
                    .First()))
                .ToHashSet();

            var markets = await broker.GetMarketsAsync(
                baseAssetFilter,
                yoloConfig.BaseCurrency,
                yoloConfig.AssetPermissions,
                cancellationToken);

            var tradeFactory = serviceProvider.GetService<ITradeFactory>()!;

            var trades = tradeFactory
                .CalculateTrades(weights, positions, markets)
                .ToArray();

            if (!trades.Any())
            {
                return Success;
            }

            if (!Silent)
            {
                _logger.LogInformation("Proceed? (y/n): ");

                if (Console.Read() != 'y')
                {
                    return Success;
                }
            }

            var returnCode = Success;

            await foreach (var result in broker.PlaceTradesAsync(trades, cancellationToken))
            {
                if (result.Success)
                {
                    _logger.PlacedOrder(result.Order!.AssetName, result.Order);
                }
                else
                {
                    _logger.OrderError(result.Trade.AssetName, result.Error, result.ErrorCode);
                    returnCode = result.ErrorCode ?? Error;
                }
            }

            return returnCode;
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