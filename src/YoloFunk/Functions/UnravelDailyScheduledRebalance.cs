using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloAbstractions.Interfaces;

namespace YoloFunk.Functions;

public class UnravelDailyScheduledRebalance
{
    private const string StrategyKey = "unraveldaily";

    private readonly ILogger<UnravelDailyScheduledRebalance> _logger;
    private readonly ICommand _rebalanceCommand;

    public UnravelDailyScheduledRebalance(
        [FromKeyedServices(StrategyKey)] ICommand rebalanceCommand,
        ILogger<UnravelDailyScheduledRebalance> logger)
    {
        _rebalanceCommand = rebalanceCommand;
        _logger = logger;
    }

    [Function(nameof(UnravelDailyScheduledRebalance))]
    public async Task Run(
        [TimerTrigger("0 30 0 * * *")] TimerInfo myTimer, // Daily at 12:30am
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
