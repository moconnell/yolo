using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloFunk.Dto;
using YoloFunk.Infrastructure;

namespace YoloFunk.Functions;

public abstract class TimerTradeIngestionFunctionBase(
    IServiceProvider serviceProvider,
    ILogger logger)
{
    protected abstract string StrategyKey { get; }

    protected async Task RunTradeIngestionAsync(
        DurableTaskClient durableClient,
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

        logger.LogInformation(
            "{Strategy} scheduled trade ingestion triggered at: {ExecutionTime}",
            StrategyKey,
            DateTime.UtcNow);

        var request = new TradeIngestionRequest(
            StrategyKey,
            "timer",
            DateTime.UtcNow);

        var startResult = await TradeIngestionDurableWorkflow.StartIfNotRunningAsync(
            durableClient,
            request,
            cancellationToken);

        if (startResult.Started)
        {
            logger.LogInformation(
                "Started durable trade ingestion orchestration {InstanceId} for strategy {Strategy}",
                startResult.InstanceId,
                StrategyKey);
        }
        else
        {
            logger.LogInformation(
                "Skipping orchestration start for strategy {Strategy}; existing instance {InstanceId} is {Status}",
                StrategyKey,
                startResult.InstanceId,
                startResult.Existing?.RuntimeStatus);
        }
    }

    protected void LogNextSchedule(DateTime? nextSchedule)
    {
        if (nextSchedule is not null)
        {
            logger.LogInformation("Next trade ingestion schedule at: {NextSchedule}", nextSchedule);
        }
    }
}
