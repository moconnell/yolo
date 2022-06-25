using YoloAbstractions;

namespace YoloRuntime;

public interface IYoloRuntime : IDisposable
{
    Task PlaceTradesAsync(IEnumerable<Trade> trades, CancellationToken cancellationToken);
    Task<IEnumerable<Trade>> RebalanceAsync(CancellationToken cancellationToken);
    IObservable<TradeResult> TradeUpdates { get; }
}