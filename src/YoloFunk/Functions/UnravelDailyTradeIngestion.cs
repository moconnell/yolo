using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace YoloFunk.Functions;

public sealed class UnravelDailyTradeIngestion(
    IServiceProvider serviceProvider,
    ILogger<UnravelDailyTradeIngestion> logger)
    : TimerTradeIngestionFunctionBase(serviceProvider, logger)
{
    private const string StrategyKeyConstant = "unraveldaily";
    protected override string StrategyKey => StrategyKeyConstant;

    [Function(nameof(UnravelDailyTradeIngestion))]
    [ExponentialBackoffRetry(3, "00:05:00", "00:30:00")]
    public async Task Run(
        [TimerTrigger("%Strategies:UnravelDaily:TradeIngestion:Schedule%")]
        TimerInfo timer,
        CancellationToken cancellationToken)
    {
        await RunTradeIngestionAsync(cancellationToken);
        LogNextSchedule(timer.ScheduleStatus?.Next);
    }
}
