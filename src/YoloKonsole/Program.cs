// See https://aka.ms/new-console-template for more information
// [Argument(short name (char), long name (string), help text)]

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
                    .AddConsole(configure =>
                        configure.LogToStandardErrorThreshold = LogLevel.Debug))
                .AddBroker(config)
                .AddSingleton<ITradeFactory, TradeFactory>()
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            _logger = serviceProvider.GetService<ILoggerFactory>()!
                .CreateLogger<Program>();
            _logger.LogInformation("************ YOLO started ************");

            var yoloConfig = config.GetYoloConfig();

            var weights = await yoloConfig
                .GetWeights();

            using IYoloBroker broker = serviceProvider.GetService<IYoloBroker>()!;
            var positions = await broker.GetPositionsAsync(cancellationToken);
            var markets = await broker.GetMarketsAsync(cancellationToken);

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

            if (Console.Read() == 'y')
            {
                await broker.PlaceTradesAsync(trades, cancellationToken);
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