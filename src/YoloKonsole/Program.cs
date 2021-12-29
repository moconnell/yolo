using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Utility.CommandLine;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloRuntime;
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

            var config = builder.Build();

            var serviceProvider = new ServiceCollection()
                .AddLogging(loggingBuilder => loggingBuilder
                    .AddFile(config.GetSection("Logging")))
                .AddBroker(config)
                .AddSingleton<ITradeFactory, TradeFactory>()
                .AddSingleton<IYoloRuntime, Runtime>()
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            _logger = serviceProvider.GetService<ILoggerFactory>()!
                .CreateLogger<Program>();
            _logger.LogInformation("************ YOLO started ************");

            var yoloConfig = config.GetYoloConfig();
            var weights = await yoloConfig.GetWeights();

            using var runtime = serviceProvider.GetService<IYoloRuntime>()!;
            runtime.TradeUpdates.Subscribe(TradeResultOnNext);
            if (!Silent)
                runtime.Challenge = ProceedChallenge; 
            
            await runtime.Rebalance(weights, cancellationToken);
            
            return Success;
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

    private static bool ProceedChallenge(IEnumerable<Trade> trades)
    {
        Console.WriteLine("Generated trades:");
        Console.WriteLine();
        foreach (var trade in trades)
            Console.WriteLine(
                $"{trade.AssetName}:\t{ToSide(trade.Amount)} {Math.Abs(trade.Amount)} at {trade.LimitPrice}");
        Console.WriteLine();
        Console.Write("Proceed? (y/n): ");

        return Console.Read() != 'y'; // return Success;
    }

    private static void TradeResultOnNext(TradeResult result)
    {
        
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