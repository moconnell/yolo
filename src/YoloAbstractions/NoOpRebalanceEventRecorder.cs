using YoloAbstractions.Interfaces;

namespace YoloAbstractions;

public sealed class NoOpRebalanceEventRecorder : IRebalanceEventRecorder
{
    public static readonly NoOpRebalanceEventRecorder Instance = new();

    private NoOpRebalanceEventRecorder()
    {
    }

    public Task RecordAsync(RebalanceEventRecord record, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
