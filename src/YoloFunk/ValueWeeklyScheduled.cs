using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloAbstractions.Interfaces;

namespace YoloFunk;

public class ValueWeeklyScheduled
{
    private const string StrategyKey = "valueweekly";

    private readonly ILogger<ValueWeeklyScheduled> _logger;
    private readonly ICommand _rebalanceCommand;

    public ValueWeeklyScheduled(
        [FromKeyedServices(StrategyKey)] ICommand rebalanceCommand,
        ILogger<ValueWeeklyScheduled> logger)
    {
        _rebalanceCommand = rebalanceCommand;
        _logger = logger;
    }

    [Function("ValueWeeklyScheduled")]
    public async Task Run(
        [TimerTrigger("0 0 1 * * 0")] TimerInfo myTimer, // Sundays at 1am
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
