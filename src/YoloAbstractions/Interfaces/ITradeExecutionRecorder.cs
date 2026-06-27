namespace YoloAbstractions.Interfaces;

public interface ITradeExecutionRecorder
{
    Task RecordAsync(TradeExecutionRecord record, CancellationToken cancellationToken = default);
}
