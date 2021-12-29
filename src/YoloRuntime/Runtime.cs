using System.Collections.Concurrent;
using System.Reactive.Subjects;
using YoloAbstractions;
using YoloBroker;
using YoloTrades;

namespace YoloRuntime;

public class Runtime : IYoloRuntime
{
    private readonly IYoloBroker _broker;
    private readonly IDictionary<string, IEnumerable<MarketInfo>> _cachedMarkets;
    private readonly IDictionary<string, Order> _cachedOrders;
    private readonly IDictionary<string, IEnumerable<Position>> _cachedPositions;
    private readonly ConcurrentDictionary<string, Weight> _cachedWeights;
    private readonly ITradeFactory _tradeFactory;
    private readonly Subject<TradeResult> _tradeResultsSubject;
    private CancellationToken _cancellationToken;
    private bool _disposed;

    public Runtime(IYoloBroker broker, ITradeFactory tradeFactory)
    {
        _tradeResultsSubject = new Subject<TradeResult>();
        _broker = broker;
        _broker.MarketUpdates.Subscribe(OnUpdate);
        _broker.OrderUpdates.Subscribe(OnUpdate);
        _broker.PositionUpdates.Subscribe(OnUpdate);
        _tradeFactory = tradeFactory;
        _cachedOrders = new ConcurrentDictionary<string, Order>();
        _cachedMarkets = new ConcurrentDictionary<string, IEnumerable<MarketInfo>>();
        _cachedPositions = new ConcurrentDictionary<string, IEnumerable<Position>>();
        _cachedWeights = new ConcurrentDictionary<string, Weight>();
    }

    private bool AllFilled => _cachedOrders.Values.All(order => order.OrderStatus == OrderStatus.Filled);

    public Func<IEnumerable<Trade>, bool> Challenge { get; set; } = _ => true;

    public IObservable<TradeResult> TradeUpdates => _tradeResultsSubject;

    public async Task Rebalance(IDictionary<string, Weight> weights, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

        var orders = await _broker.GetOrdersAsync(cancellationToken);

        if (orders.Any()) throw new OpenOrdersException("Open orders!", orders.Values);

        weights.CopyTo(_cachedWeights);

        var positions = await _broker.GetPositionsAsync(cancellationToken);

        positions.CopyTo(_cachedPositions);

        var baseAssetFilter = positions
            .Keys
            .ToHashSet();

        var markets = await _broker.GetMarketsAsync(
            baseAssetFilter,
            cancellationToken);

        markets.CopyTo(_cachedMarkets);

        if (await CalculateTrades(_cachedWeights, cancellationToken)) return;

        // TODO: set timeout on each to recalculate if not filled (update limit price?)

        while (!AllFilled) await Task.Delay(1000, cancellationToken);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private async Task<bool> CalculateTrades(IDictionary<string, Weight> weights, CancellationToken cancellationToken)
    {
        var trades = _tradeFactory
            .CalculateTrades(weights, _cachedPositions, _cachedMarkets)
            .OrderBy(trade => trade.AssetName)
            .ToArray();

        if (!Challenge(trades))
            return false;

        await foreach (var result in _broker.PlaceTradesAsync(trades, cancellationToken))
        {
            if (result.Order is { })
                _cachedOrders[result.Trade.BaseAsset] = result.Order;

            OnNext(result);
        }

        return true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _broker.Dispose();
            _tradeResultsSubject.Dispose();
        }

        _disposed = true;
    }

    private void OnNext(TradeResult result) => _tradeResultsSubject.OnNext(result);

    private void OnUpdate(MarketInfo marketInfo)
    {
        var baseAsset = marketInfo.BaseAsset;
        var baseAssetMarkets = _cachedMarkets[baseAsset]
            .ToDictionary(mkt => mkt.Name, mkt => mkt);
        baseAssetMarkets[marketInfo.Name] = marketInfo;
        _cachedMarkets[baseAsset] = baseAssetMarkets.Values;
    }

    private void OnUpdate(OrderUpdate update)
    {
        var (trade, order) = update;

        _cachedOrders[trade.BaseAsset] = order;

        _tradeResultsSubject.OnNext(new TradeResult(trade, true, order));

        if (order.OrderStatus == OrderStatus.Cancelled) Resubmit(trade);
    }

    private void OnUpdate(Position position)
    {
        var baseAsset = position.BaseAsset;
        var baseAssetPositions = _cachedPositions[baseAsset]
            .ToDictionary(pos => pos.AssetName, pos => pos);
        baseAssetPositions[position.AssetName] = position;
        _cachedPositions[baseAsset] = baseAssetPositions.Values;
    }

    private async Task Resubmit(Trade trade)
    {
        var baseAsset = trade.BaseAsset;
        var weight = _cachedWeights[baseAsset];
        await CalculateTrades(new Dictionary<string, Weight> {{baseAsset, weight}}, _cancellationToken);
    }
}