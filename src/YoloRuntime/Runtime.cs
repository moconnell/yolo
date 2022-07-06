using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloBroker;
using YoloTrades;
using YoloWeights;
using Weight = YoloAbstractions.Weight;

namespace YoloRuntime;

public class Runtime : IYoloRuntime
{
    private readonly IYoloBroker _broker;
    private readonly ConcurrentDictionary<string, IEnumerable<MarketInfo>> _cachedMarkets;
    private readonly ConcurrentDictionary<string, IEnumerable<Position>> _cachedPositions;
    private readonly ConcurrentDictionary<string, IDictionary<string, TradeResult>> _cachedTradeResults;
    private readonly ConcurrentDictionary<string, Weight> _cachedWeights;
    private readonly ILogger<Runtime> _logger;
    private readonly IDisposable _marketUpdatesSubscription;
    private readonly IDisposable _orderUpdatesSubscription;
    private readonly IDisposable _positionUpdatesSubscription;
    private readonly ITradeFactory _tradeFactory;
    private readonly Subject<(string baseAsset, IEnumerable<TradeResult> results)> _tradeResultsSubject;
    private readonly IYoloWeightsService _weightsService;
    private readonly IDisposable _tradeResultsSubscription;
    private CancellationToken _cancellationToken;
    private bool _disposed;

    public Runtime(
        IYoloWeightsService weightsService,
        IYoloBroker broker,
        ITradeFactory tradeFactory,
        YoloConfig config,
        ILogger<Runtime> logger)
    {
        _tradeResultsSubject = new Subject<(string baseAsset, IEnumerable<TradeResult> results)>();
        _weightsService = weightsService;
        _broker = broker;
        _marketUpdatesSubscription = _broker.MarketUpdates.Subscribe(OnUpdate);
        _orderUpdatesSubscription = _broker.OrderUpdates.SelectMany(OnUpdate).Subscribe();
        _positionUpdatesSubscription = _broker.PositionUpdates.Subscribe(OnUpdate);
        _tradeFactory = tradeFactory;
        _logger = logger;
        _cachedMarkets = new ConcurrentDictionary<string, IEnumerable<MarketInfo>>();
        _cachedPositions = new ConcurrentDictionary<string, IEnumerable<Position>>();
        _cachedWeights = new ConcurrentDictionary<string, Weight>();
        _cachedTradeResults = new ConcurrentDictionary<string, IDictionary<string, TradeResult>>();
        _tradeResultsSubscription = _tradeResultsSubject
            .Subscribe(OnUpdate);

        UnfilledOrderTimeout = config.UnfilledOrderTimeout;
    }


    public TimeSpan? UnfilledOrderTimeout { get; set; }

    public IObservable<(string baseAsset, IEnumerable<TradeResult> results)> TradeUpdates =>
        _tradeResultsSubject;

    public async Task<IEnumerable<IGrouping<string, Trade>>> RebalanceAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

        var orders = await _broker.GetOrdersAsync(cancellationToken);

        if (orders.Any())
        {
            throw new OpenOrdersException("Open orders!", orders.Values);
        }

        var positions = await _broker.GetPositionsAsync(cancellationToken);

        positions.CopyTo(_cachedPositions);

        var weights = await UpdateWeights(cancellationToken);

        var baseAssetFilter = positions.Keys
            .Union(weights.Keys)
            .ToHashSet();

        var markets = await _broker.GetMarketsAsync(
            baseAssetFilter,
            cancellationToken);

        markets.CopyTo(_cachedMarkets);

        return CalculateTrades(_cachedWeights);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async Task PlaceTradesAsync(IEnumerable<Trade> trades, CancellationToken cancellationToken)
    {
        await PlaceTradesImplAsync(trades, cancellationToken);

        if (UnfilledOrderTimeout.HasValue)
        {
            while (true)
            {
                await Task.Delay(UnfilledOrderTimeout.Value, cancellationToken);
                var unfilledOrders = UnfilledOrders;
                if (!unfilledOrders.Any())
                    return;

                await CancelOrdersAsync(unfilledOrders);
                await UpdateWeights(cancellationToken);
                var groupings = CalculateTrades(_cachedWeights);
                await PlaceTradesImplAsync(groupings.SelectMany(g => g.ToArray()), cancellationToken);
            }
        }
    }

    private Order[] UnfilledOrders
    {
        get
        {
            return _cachedTradeResults
                .Values
                .SelectMany(results => results.Values)
                .Where(tr => tr.Order?.OrderStatus is OrderStatus.New or OrderStatus.Open)
                .Select(tr => tr.Order)
                .Cast<Order>()
                .ToArray();
        }
    }

    private async Task CancelOrdersAsync(IEnumerable<Order> orders)
    {
        foreach (var order in orders)
        {
            await _broker.CancelOrderAsync(order.Id, _cancellationToken);
        }
    }

    private async Task<IDictionary<string, Weight>> UpdateWeights(CancellationToken cancellationToken)
    {
        var weights = await _weightsService.GetWeightsAsync(cancellationToken);
        _cachedWeights.Clear();
        weights.CopyTo(_cachedWeights);
        return weights;
    }

    private IGrouping<string, Trade>[] CalculateTrades(IDictionary<string, Weight> weights)
    {
        var groups = _tradeFactory
            .CalculateTrades(weights, _cachedPositions, _cachedMarkets)
            .OrderBy(g => g.Key)
            .ToArray();

        foreach (var g in groups)
        {
            OnNext(g.Key, g.Select(t => new TradeResult(t)));
        }

        return groups;
    }

    private async Task PlaceTradesImplAsync(IEnumerable<Trade> trades, CancellationToken cancellationToken)
    {
        await foreach (var result in _broker.PlaceTradesAsync(trades, cancellationToken))
        {
            OnNext(result);
        }
    }

    private void OnNext(TradeResult result)
    {
        if (!_cachedTradeResults.TryGetValue(result.Trade.BaseAsset, out var tradeResults))
        {
            return;
        }

        tradeResults[result.Trade.AssetName] = result;
        OnNext(result.Trade.BaseAsset, tradeResults.Values);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _broker.Dispose();
            _marketUpdatesSubscription.Dispose();
            _orderUpdatesSubscription.Dispose();
            _positionUpdatesSubscription.Dispose();
            _tradeResultsSubject.Dispose();
            _tradeResultsSubscription.Dispose();
        }

        _disposed = true;
    }

    private void OnNext(string baseAsset, IEnumerable<TradeResult> results) =>
        _tradeResultsSubject.OnNext((baseAsset, results));

    private void OnUpdate(MarketInfo marketInfo)
    {
        var baseAsset = marketInfo.BaseAsset;

        if (!_cachedMarkets.TryGetValue(baseAsset, out var markets))
        {
            return;
        }

        var baseAssetMarkets = markets.ToDictionary(mkt => mkt.Name, mkt => mkt);
        baseAssetMarkets[marketInfo.Name] = marketInfo;
        _cachedMarkets[baseAsset] = baseAssetMarkets.Values;

        if (_cachedTradeResults.TryGetValue(baseAsset, out var results) &&
            results.Values.All(tr => tr.Order?.OrderStatus is not OrderStatus.New or OrderStatus.Open))
        {
            RecalculateTrades(baseAsset);
        }
    }

    private async Task<Unit> OnUpdate(OrderUpdate update)
    {
        var (trade, order) = update;

        OnNext(new TradeResult(trade, true, order));

        if (order.OrderStatus == OrderStatus.Cancelled)
        {
            _logger.OrderCancelled(order);
            await Resubmit(trade);
        }

        return Unit.Default;
    }

    private void OnUpdate(Position position)
    {
        var baseAsset = position.BaseAsset;
        var baseAssetPositions = _cachedPositions[baseAsset]
            .ToDictionary(pos => pos.AssetName, pos => pos);
        baseAssetPositions[position.AssetName] = position;
        _cachedPositions[baseAsset] = baseAssetPositions.Values;
    }

    private void OnUpdate((string baseAsset, IEnumerable<TradeResult> results) update)
    {
        var (baseAsset, results) = update;
        _cachedTradeResults[baseAsset] = results.ToDictionary(tr => tr.Trade.AssetName);
    }

    private async Task Resubmit(Trade trade)
    {
        var newTrades = RecalculateTrades(trade.BaseAsset);
        await PlaceTradesAsync(newTrades, _cancellationToken);
    }

    private IEnumerable<Trade> RecalculateTrades(string baseAsset)
    {
        var weight = _cachedWeights[baseAsset];
        var groupings = CalculateTrades(new Dictionary<string, Weight> { { baseAsset, weight } });
        
        return groupings.First();
    }
}