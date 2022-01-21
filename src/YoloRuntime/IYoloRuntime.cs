using YoloAbstractions;

namespace YoloRuntime;

public interface IYoloRuntime : IDisposable
{
    Task Rebalance(IDictionary<string, Weight> weights, CancellationToken cancellationToken);

    IObservable<TradeResult> TradeUpdates { get; }
    Func<IReadOnlyList<Trade>, bool> Challenge { get; set; }
}