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

        if (!Silent)
            Console.Write(@"
__  ______  __    ____  __
\ \/ / __ \/ /   / __ \/ /
 \  / / / / /   / / / / / 
 / / /_/ / /___/ /_/ /_/  
/_/\____/_____/\____(_)   
                          
");

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
                    .AddFile(config.GetSection("Logging")))
                .AddBroker(config)
                .AddSingleton<ITradeFactory, TradeFactory>()
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            _logger = serviceProvider.GetService<ILoggerFactory>()!
                .CreateLogger<Program>();
            _logger.LogInformation("************ YOLO started ************");

            var yoloConfig = config.GetYoloConfig();

            var weights = (await yoloConfig.GetWeights()).ToArray();

            using var broker = serviceProvider.GetService<IYoloBroker>()!;

            var orders = await broker.GetOrdersAsync(cancellationToken);

            if (orders.Any())
            {
                _logger.OpenOrders(orders.Values);

                if (!Silent)
                {
                    Console.WriteLine("Open orders!");
                    Console.WriteLine();
                    foreach (var order in orders.Values)
                        Console.WriteLine(
                            $"{order.AssetName}:\t{ToSide(order.Amount)} {Math.Abs(order.Amount)} at {order.LimitPrice}");
                }

                return Error;
            }

            var positions = await broker.GetPositionsAsync(cancellationToken);

            var baseAssetFilter = positions
                .Keys
                .ToHashSet();

            var markets = await broker.GetMarketsAsync(
                baseAssetFilter,
                yoloConfig.BaseAsset,
                yoloConfig.AssetPermissions,
                cancellationToken);

            var tradeFactory = serviceProvider.GetService<ITradeFactory>()!;

            var trades = tradeFactory
                .CalculateTrades(weights, positions, markets)
                .OrderBy(trade => trade.AssetName)
                .ToArray();

            if (!trades.Any())
            {
                if (!Silent) Console.WriteLine("Nothing to do.");

                return Success;
            }

            if (!Silent)
            {
                Console.WriteLine("Generated trades:");
                Console.WriteLine();
                foreach (var trade in trades)
                    Console.WriteLine(
                        $"{trade.AssetName}:\t{ToSide(trade.Amount)} {Math.Abs(trade.Amount)} at {trade.LimitPrice}");
                Console.WriteLine();
                Console.Write("Proceed? (y/n): ");

                if (Console.Read() != 'y') return Success;
            }

            var returnCode = Success;

            await foreach (var result in broker.PlaceTradesAsync(trades, cancellationToken))
                if (result.Success)
                {
                    var order = result.Order!;
                    _logger.PlacedOrder(order.AssetName, order);
                    if (!Silent)
                        Console.WriteLine(
                            $"{order.AssetName}:\t{order.OrderSide} {Math.Abs(order.Amount)} at {order.LimitPrice}\t({order.OrderStatus})");
                }
                else
                {
                    _logger.OrderError(result.Trade.AssetName, result.Error, result.ErrorCode);
                    if (!Silent)
                        Console.WriteLine($"{result.Trade.AssetName}:\t{result.Error}");
                    returnCode = result.ErrorCode ?? Error;
                }

            return returnCode;
        }
        catch (Exception e)
        {
            source.Cancel();

            _logger?.LogError("Error occurred: {Error}", e);

            Console.WriteLine(e.Message);

            return Error;
        }
        finally
        {
            _logger?.LogInformation("************ YOLO ended ************");
        }
    }

    private static string ToSide(decimal amount) => amount >= 0 ? "BUY" : "SELL";
}