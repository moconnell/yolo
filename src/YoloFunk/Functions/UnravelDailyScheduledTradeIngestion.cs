using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace YoloFunk.Functions;

public sealed class UnravelDailyScheduledTradeIngestion(
    IServiceProvider serviceProvider,
    ILogger<UnravelDailyScheduledTradeIngestion> logger)
    : TimerTradeIngestionFunctionBase(serviceProvider, logger)
{
    private const string StrategyKeyConstant = "unraveldaily";
    protected override string StrategyKey => StrategyKeyConstant;

    [Function(nameof(UnravelDailyScheduledTradeIngestion))]
    [ExponentialBackoffRetry(3, "00:05:00", "00:30:00")]
    public async Task Run(
        [TimerTrigger("%Strategies:UnravelDaily:TradeIngestion:Schedule%")]
        TimerInfo timer,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        await RunTradeIngestionAsync(durableClient, cancellationToken);
        LogNextSchedule(timer.ScheduleStatus?.Next);
    }
}
