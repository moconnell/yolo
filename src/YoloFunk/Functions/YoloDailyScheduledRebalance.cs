using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloAbstractions.Interfaces;

namespace YoloFunk.Functions;

public class YoloDailyScheduledRebalance
{
    private const string StrategyKey = "yolodaily"; // Match your config section name

    private readonly ILogger<YoloDailyScheduledRebalance> _logger;
    private readonly ICommand _rebalanceCommand;

    public YoloDailyScheduledRebalance(
        [FromKeyedServices(StrategyKey)] ICommand rebalanceCommand,
        ILogger<YoloDailyScheduledRebalance> logger)
    {
        _rebalanceCommand = rebalanceCommand;
        _logger = logger;
    }

    [Function(nameof(YoloDailyScheduledRebalance))]
    [ExponentialBackoffRetry(3, "00:05:00", "00:30:00")]
    public async Task Run(
        [TimerTrigger("%Strategies:YoloDaily:Schedule%")] TimerInfo myTimer,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Strategy} scheduled rebalance executed at: {executionTime}",
            StrategyKey, DateTime.UtcNow);

        await _rebalanceCommand.ExecuteAsync(cancellationToken);

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }
    }
}