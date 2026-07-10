using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace YoloFunk.Functions;

public sealed class YoloDailyScheduledTradeIngestion(
    IServiceProvider serviceProvider,
    ILogger<YoloDailyScheduledTradeIngestion> logger)
    : TimerTradeIngestionFunctionBase(serviceProvider, logger)
{
    private const string StrategyKeyConstant = "yolodaily";
    protected override string StrategyKey => StrategyKeyConstant;

    [Function(nameof(YoloDailyScheduledTradeIngestion))]
    [ExponentialBackoffRetry(3, "00:05:00", "00:30:00")]
    public async Task Run(
        [TimerTrigger("%Strategies:YoloDaily:TradeIngestion:Schedule%")]
        TimerInfo timer,
        CancellationToken cancellationToken)
    {
        await RunTradeIngestionAsync(cancellationToken);
        LogNextSchedule(timer.ScheduleStatus?.Next);
    }
}
