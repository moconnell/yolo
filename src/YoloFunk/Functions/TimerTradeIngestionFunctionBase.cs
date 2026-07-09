using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloFunk.Infrastructure;

namespace YoloFunk.Functions;

public abstract class TimerTradeIngestionFunctionBase(
    IServiceProvider serviceProvider,
    ILogger logger)
{
    protected abstract string StrategyKey { get; }

    protected async Task RunTradeIngestionAsync(CancellationToken cancellationToken)
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
    }

    protected void LogNextSchedule(DateTime? nextSchedule)
    {
        if (nextSchedule is not null)
        {
            logger.LogInformation("Next trade ingestion schedule at: {NextSchedule}", nextSchedule);
        }
    }
}
