using YoloAbstractions.Interfaces;

namespace YoloAbstractions;

public sealed class NoOpTradeExecutionRecorder : ITradeExecutionRecorder
{
    public static readonly NoOpTradeExecutionRecorder Instance = new();

    private NoOpTradeExecutionRecorder()
    {
    }

    public Task RecordAsync(TradeExecutionRecord record, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
