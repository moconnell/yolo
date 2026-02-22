using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using YoloFunk.Dto;

namespace YoloFunk.Functions;

public class YoloDailyScheduledRebalance
{
    private const string StrategyKey = "yolodaily";

    private readonly ILogger<YoloDailyScheduledRebalance> _logger;

    public YoloDailyScheduledRebalance(ILogger<YoloDailyScheduledRebalance> logger)
    {
        _logger = logger;
    }

    [Function(nameof(YoloDailyScheduledRebalance))]
    [ExponentialBackoffRetry(3, "00:05:00", "00:30:00")]
    public async Task Run(
        [TimerTrigger("%Strategies:YoloDaily:Schedule%")]
        TimerInfo myTimer,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Strategy} scheduled rebalance executed at: {executionTime}",
            StrategyKey, DateTime.UtcNow);

        var request = new RebalanceRequest(
            StrategyKey,
            "timer",
            DateTime.UtcNow);

        var startResult = await RebalanceDurableWorkflow.StartIfNotRunningAsync(
            durableClient,
            request,
            cancellationToken);

        if (startResult.Started)
        {
            _logger.LogInformation(
                "Started durable rebalance orchestration {InstanceId} for strategy {Strategy}",
                startResult.InstanceId,
                StrategyKey);
        }
        else
        {
            _logger.LogInformation(
                "Skipping orchestration start for strategy {Strategy}; existing instance {InstanceId} is {Status}",
                StrategyKey,
                startResult.InstanceId,
                startResult.Existing?.RuntimeStatus);
        }

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }
    }
}
