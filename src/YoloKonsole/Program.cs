using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Utility.CommandLine;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloBroker.Interface;
using YoloTrades;
using YoloWeights;
using static YoloKonsole.WellKnown.TradeEventIds;

namespace YoloKonsole;

internal class Program
{
    private const int Error = 1;
    private const int Success = 0;
    private const string F5 = "F5";
    private static ILogger<Program>? _logger;

    [Argument('s', "silent", "Silent running: no challenge will be issued before placing trades")]
    internal static bool Silent { get; set; }

    private static async Task<int> Main(string[] args)
    {
#if DEBUG
        var env = "local";
#else
        var env = "prod";
#endif

        Arguments.Populate();

        ConsoleWrite(@"
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

            // add file-based secrets
            var secretsPath = Environment.GetEnvironmentVariable("YOLO_SECRETS_PATH");
            if (secretsPath != null && Directory.Exists(secretsPath))
            {
                builder.AddKeyPerFile(secretsPath, optional: true);
            }

            var config = builder.Build();

            var serviceProvider = new ServiceCollection()
                .AddLogging(loggingBuilder => loggingBuilder
                    .AddFile(config.GetSection("Logging"))
                    )
                .AddBroker(config)
                .AddSingleton<ITradeFactory, TradeFactory>()
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            _logger = serviceProvider.GetService<ILoggerFactory>()!
                .CreateLogger<Program>();
            _logger.LogInformation("************ YOLO started ************");

            var yoloConfig = config.GetYoloConfig() ?? throw new ConfigException("YOLO configuration is missing or invalid");
            var weights = await yoloConfig.GetWeights();

            using var broker = serviceProvider.GetService<IYoloBroker>()!;

            var orders = await broker.GetOpenOrdersAsync(cancellationToken);

            if (orders.Count != 0)
            {
                _logger.OpenOrders(orders.Values);

                if (!Silent)
                {
                    Console.WriteLine("Open orders!");
                    Console.WriteLine();
                    foreach (var order in orders.Values)
                        Console.WriteLine(
                            $"{order.Symbol}:\t{ToSide(order.Amount)} {Math.Abs(order.Amount)} at {order.LimitPrice}");
                }

                return OpenOrders;
            }

            var positions = await broker.GetPositionsAsync(cancellationToken);

            var baseAssetFilter = positions
                .Keys
                .Union(weights.Select(w => w.Ticker.GetBaseAndQuoteAssets().BaseAsset))
                .ToHashSet();

            var markets = await broker.GetMarketsAsync(
                baseAssetFilter,
                yoloConfig.BaseAsset,
                yoloConfig.AssetPermissions,
                cancellationToken);

            var tradeFactory = serviceProvider.GetService<ITradeFactory>()!;

            var trades = tradeFactory
                .CalculateTrades(weights, positions, markets)
                .OrderBy(trade => trade.Symbol)
                .ToArray();

            if (trades.Length == 0)
            {
                ConsoleWriteLine("Nothing to do.");

                return Success;
            }

            if (!Silent)
            {
                Console.WriteLine("Generated trades:");
                Console.WriteLine();
                foreach (var trade in trades)
                    Console.WriteLine(
                        $"{trade.Symbol} [{trade.AssetType}]:\t{ToSide(trade.Amount)} {Math.Abs(trade.Amount)} at {trade.LimitPrice.ToString() ?? "Market"}");
                Console.WriteLine();
                Console.Write("Proceed? (y/n): ");

                if (Console.Read() != 'y') return Success;
            }

            var returnCode = Success;

            var (table, index) = CreateOrderTable(trades);

            await AnsiConsole.Live(table)
                .StartAsync(async ctx =>
                {
                    ctx.UpdateTarget(table);

                    var settings = OrderManagementSettings.Default with
                    {
                        UnfilledOrderTimeout = TimeSpan.TryParse(yoloConfig.UnfilledOrderTimeout, out var timeout)
                                        ? timeout
                                        : OrderManagementSettings.Default.UnfilledOrderTimeout
                    };

                    _logger.LogInformation("Managing orders for {TradeCount} trades", trades.Length);

                    try
                    {
                        await foreach (var update in broker.ManageOrdersAsync(trades, settings, cancellationToken))
                        {
                            UpdateOrderTable(table, index, update);

                            if (update.Type == OrderUpdateType.Error)
                            {
                                _logger.OrderError(update.Symbol, update.Message, Error);
                                returnCode = new[] { Error, returnCode }.Max();
                                AnsiConsole.MarkupLine($"[red]Error on {update.Symbol}: {update.Error?.Message}[/]");
                            }

                            ctx.Refresh();
                        }
                    }
                    catch (ChannelClosedException)
                    {
                        // Expected cancellation, we can ignore this
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected cancellation, we can ignore this
                    }

                });

            return returnCode;
        }
        catch (Exception e)
        {
            await source.CancelAsync();

            _logger?.LogError("Error occurred: {Error}", e);

            Console.WriteLine(e.Message);

            return Error;
        }
        finally
        {
            _logger?.LogInformation("************ YOLO ended ************");
        }
    }

    private static (Table table, ConcurrentDictionary<string, int> index) CreateOrderTable(Trade[] trades)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .Title("[bold]Orders[/]")
            .Expand()
            .AddColumn("Symbol", c => c.Centered())
            .AddColumn("Type", c => c.Centered())
            .AddColumn("Side", c => c.Centered())
            .AddColumn("Amount", c => c.RightAligned().NoWrap())
            .AddColumn("Filled", c => c.RightAligned().NoWrap())
            .AddColumn("Price", c => c.RightAligned().NoWrap())
            .AddColumn("Status", c => c.Centered().NoWrap())
            .AddColumn("Updated", c => c.RightAligned().NoWrap());

        var index = new ConcurrentDictionary<string, int>();

        foreach (var trade in trades)
        {
            var assetType = trade.AssetType.ToString();
            var side = trade.OrderSide.ToString();
            var amount = trade.AbsoluteAmount.ToString(F5);
            var filled = string.Empty;
            var price = trade.LimitPrice?.ToString(F5) ?? "Market";
            var status = "[yellow]Pending[/]";
            var time = DateTime.Now.ToString("HH:mm:ss");

            table.AddRow(
                trade.Symbol,
                assetType,
                side,
                amount,
                filled,
                price,
                status,
                time);

            index[trade.Symbol] = table.Rows.Count - 1;
        }

        return (table, index);
    }

    private static void UpdateOrderTable(Table table, ConcurrentDictionary<string, int> index, OrderUpdate update)
    {
        // Find existing row for this asset or add new one
        var existingRowIndex = FindRowByAsset(table, index, update.Symbol);

        if (existingRowIndex >= 0)
        {
            var assetType = update.Order?.AssetType.ToString() ?? " ";
            var side = update.Order?.OrderSide.ToString() ?? " ";
            var amount = update.Order?.Amount.ToString(F5) ?? " ";
            var filled = update.Order?.Filled?.ToString(F5) ?? " ";
            var price = update.Order?.LimitPrice?.ToString(F5) ?? "Market";
            var status = GetStatusMarkup(update.Type);
            var time = DateTime.Now.ToString("HH:mm:ss");

            // Update existing row
            table.UpdateCell(existingRowIndex, 0, update.Symbol);
            table.UpdateCell(existingRowIndex, 1, assetType);
            table.UpdateCell(existingRowIndex, 2, side);
            table.UpdateCell(existingRowIndex, 3, amount);
            table.UpdateCell(existingRowIndex, 4, filled);
            table.UpdateCell(existingRowIndex, 5, price);
            table.UpdateCell(existingRowIndex, 6, status);
            table.UpdateCell(existingRowIndex, 7, time);
        }
    }

    private static int FindRowByAsset(Table table, ConcurrentDictionary<string, int> index, string assetName)
    {
        if (index.TryGetValue(assetName, out var rowIndex))
        {
            return rowIndex;
        }

        return -1;
    }

    private static string GetStatusMarkup(OrderUpdateType type)
    {
        return type switch
        {
            OrderUpdateType.Created => "[yellow]Created[/]",
            OrderUpdateType.PartiallyFilled => "[orange1]Partial[/]",
            OrderUpdateType.Filled => "[green]Filled[/]",
            OrderUpdateType.Cancelled => "[gray]Cancelled[/]",
            OrderUpdateType.TimedOut => "[red]Timeout[/]",
            OrderUpdateType.MarketOrderPlaced => "[blue]Market[/]",
            OrderUpdateType.Error => "[red]Error[/]",
            _ => "[gray]Unknown[/]"
        };
    }

    private static void ConsoleWrite(string s)
    {
        if (!Silent)
            Console.Write(s);
    }

    private static void ConsoleWriteLine(string s)
    {
        if (!Silent)
            Console.WriteLine(s);
    }

    private static string ToSide(decimal amount) => amount >= 0 ? "Buy" : "Sell";
}