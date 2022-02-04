using System;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Terminal.Gui;
using Utility.CommandLine;
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

        CancellationTokenSource cancellationTokenSource = new();

        try
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{env}.json", true, true)
                .AddEnvironmentVariables();

            var config = builder.Build();
            var yoloConfig = config.GetYoloConfig();

            var serviceProvider = new ServiceCollection()
                .AddLogging(loggingBuilder => loggingBuilder
                    .AddFile(config.GetSection("Logging")))
                .AddBroker(config)
                .AddSingleton(config.GetYoloConfig())
                .AddSingleton<IYoloWeightsService, YoloWeightsService>()
                .AddSingleton<ITradeFactory, TradeFactory>()
                .AddSingleton<IYoloRuntime, Runtime>()
                .AddSingleton<IConfiguration>(config)
                .AddSingleton(yoloConfig)
                .BuildServiceProvider();

            _logger = serviceProvider.GetService<ILoggerFactory>()!
                .CreateLogger<Program>();
            _logger.LogInformation("************ YOLO started ************");


            using var runtime = serviceProvider.GetService<IYoloRuntime>()!;

            Application.Init();
            RxApp.MainThreadScheduler = TerminalScheduler.Default;
            RxApp.TaskpoolScheduler = TaskPoolScheduler.Default;
            Application.Run(new YoloView(new YoloViewModel(runtime, cancellationTokenSource)));
            Application.Shutdown();

            return Success;
        }
        catch (Exception e)
        {
            cancellationTokenSource.Cancel();

            _logger?.LogError("Error occurred: {Error}", e);

            Console.WriteLine(e.Message);

            return Error;
        }
        finally
        {
            _logger?.LogInformation("************ YOLO ended ************");
        }
    }
    
    private static void ConsoleWrite(string s)
    {
        if (!Silent)
            Console.Write(s);
    }
}