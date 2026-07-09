using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloFunk.Infrastructure;

namespace YoloFunk.Functions;

public sealed class UnravelDailyTradeIngestion(
    IServiceProvider serviceProvider,
    ILogger<UnravelDailyTradeIngestion> logger)
{
    private const string StrategyKey = "unraveldaily";

    [Function(nameof(UnravelDailyTradeIngestion))]
    [ExponentialBackoffRetry(3, "00:05:00", "00:30:00")]
    public async Task Run(
        [TimerTrigger("%Strategies:UnravelDaily:TradeIngestion:Schedule%")]
        TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var ingestionService = serviceProvider.GetKeyedService<IUserTradeIngestionService>(StrategyKey);
        if (ingestionService is null)
        {
            logger.LogWarning(
                "Skipping trade ingestion for {Strategy}; AzureWebJobsStorage is not configured",
                StrategyKey);
            return;
        }

        var result = await ingestionService.IngestAsync(cancellationToken);

        logger.LogInformation(
            "Completed trade ingestion for {Strategy}: {TradeCount} trade(s) across {WindowCount} window(s), {StartUtc} to {EndUtc}",
            result.StrategyName,
            result.TradeCount,
            result.WindowCount,
            result.StartUtc,
            result.EndUtc);

        if (timer.ScheduleStatus is not null)
        {
            logger.LogInformation("Next trade ingestion schedule at: {NextSchedule}", timer.ScheduleStatus.Next);
        }
    }
}
