namespace YoloAbstractions.Interfaces;

public interface IRebalanceEventRecorder
{
    Task RecordAsync(RebalanceEventRecord record, CancellationToken cancellationToken = default);
}
