using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YoloAbstractions;

namespace YoloBroker;

public interface IYoloBroker : IDisposable
{
    Task<IReadOnlyDictionary<long, Order>> GetOrdersAsync(CancellationToken ct);
    
    Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, Position>>> GetPositionsAsync(CancellationToken ct);

    IAsyncEnumerable<TradeResult> PlaceTradesAsync(IEnumerable<Trade> trades, CancellationToken ct);

    Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, MarketInfo>>> GetMarketsAsync(
        ISet<string>? baseAssetFilter = null,
        CancellationToken ct = default);

    IObservable<MarketInfo> MarketUpdates { get; }
    IObservable<OrderUpdate> OrderUpdates { get; }
    IObservable<Position> PositionUpdates { get; }
    Task CancelOrderAsync(long orderId, CancellationToken ct);
}