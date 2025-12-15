namespace YoloAbstractions.Interfaces;

public interface ICommand
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}