using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloAbstractions.Interfaces;

namespace YoloFunk;

public class YoloScheduledRebalance
{
    private const string StrategyKey = "momentumdaily"; // Match your config section name

    private readonly ILogger<YoloScheduledRebalance> _logger;
    private readonly ICommand _rebalanceCommand;

    public YoloScheduledRebalance(
        [FromKeyedServices(StrategyKey)] ICommand rebalanceCommand,
        ILogger<YoloScheduledRebalance> logger)
    {
        _rebalanceCommand = rebalanceCommand;
        _logger = logger;
    }

    [Function("MomentumDailyScheduled")]
    public async Task Run([TimerTrigger("0 30 0 * * *")] TimerInfo myTimer, CancellationToken cancellationToken)
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