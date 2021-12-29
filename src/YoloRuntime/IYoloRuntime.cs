using YoloAbstractions;

namespace YoloRuntime;

public interface IYoloRuntime : IDisposable
{
    Task Rebalance(IDictionary<string, Weight> weights, CancellationToken cancellationToken);

    IObservable<TradeResult> TradeUpdates { get; }
    Func<IEnumerable<Trade>, bool> Challenge { get; set; }
}