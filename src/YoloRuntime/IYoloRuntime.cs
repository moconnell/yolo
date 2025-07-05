using YoloAbstractions;

namespace YoloRuntime;

public interface IYoloRuntime : IDisposable
{
    Task PlaceTradesAsync(IEnumerable<Trade> trades, CancellationToken cancellationToken);

    Task<IEnumerable<IGrouping<string, Trade>>> RebalanceAsync(CancellationToken cancellationToken);

    IObservable<(string token,
        IReadOnlyDictionary<string, TradeResult> results,
        IReadOnlyDictionary<string, Position> positions)> TokenUpdates { get; }
}