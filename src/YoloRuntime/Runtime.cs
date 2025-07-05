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
    private readonly ConcurrentDictionary<string, IDictionary<string, MarketInfo>> _cachedMarkets;
    private readonly ConcurrentDictionary<string, IDictionary<string, Position>> _cachedPositions;
    private readonly ConcurrentDictionary<string, IDictionary<string, TradeResult>> _cachedTradeResults;
    private readonly ConcurrentDictionary<string, Weight> _cachedWeights;
    private readonly ILogger<Runtime> _logger;
    private readonly IDisposable _marketUpdatesSubscription;
    private readonly IDisposable _orderUpdatesSubscription;
    private readonly IDisposable _positionUpdatesSubscription;
    private readonly ITradeFactory _tradeFactory;

    private readonly
        Subject<(string token, IReadOnlyDictionary<string, TradeResult> results, IReadOnlyDictionary<string, Position>
            positions)> _tokenSubject;

    private readonly IYoloWeightsService _weightsService;
    private CancellationToken _cancellationToken;
    private bool _disposed;

    public Runtime(
        IYoloWeightsService weightsService,
        IYoloBroker broker,
        ITradeFactory tradeFactory,
        YoloConfig config,
        ILogger<Runtime> logger)
    {
        _tokenSubject =
            new Subject<(string baseAsset, IReadOnlyDictionary<string, TradeResult> results,
                IReadOnlyDictionary<string, Position> positions)>();
        _weightsService = weightsService;
        _broker = broker;
        _marketUpdatesSubscription = _broker.MarketUpdates.Subscribe(OnUpdate);
        _orderUpdatesSubscription = _broker.OrderUpdates.SelectMany(OnUpdate).Subscribe();
        _positionUpdatesSubscription = _broker.PositionUpdates.Subscribe(OnUpdate);
        _tradeFactory = tradeFactory;
        _logger = logger;
        _cachedMarkets = new ConcurrentDictionary<string, IDictionary<string, MarketInfo>>();
        _cachedPositions = new ConcurrentDictionary<string, IDictionary<string, Position>>();
        _cachedTradeResults = new ConcurrentDictionary<string, IDictionary<string, TradeResult>>();
        _cachedWeights = new ConcurrentDictionary<string, Weight>();

        UnfilledOrderTimeout = config.UnfilledOrderTimeout;
    }


    public TimeSpan? UnfilledOrderTimeout { get; set; }

    public IObservable<(string token, IReadOnlyDictionary<string, TradeResult> results,
        IReadOnlyDictionary<string, Position> positions )> TokenUpdates => _tokenSubject;

    public async Task<IEnumerable<IGrouping<string, Trade>>> RebalanceAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

        var orders = await _broker.GetOrdersAsync(cancellationToken);

        if (orders.Any())
        {
            throw new OpenOrdersException("Open orders!", orders.Values);
        }

        var positions = await _broker.GetPositionsAsync(cancellationToken);
        positions.CopyDictOfDictTo(_cachedPositions);

        var weights = await UpdateWeights(cancellationToken);

        var baseAssetFilter = positions.Keys
            .Union(weights.Keys)
            .ToHashSet();

        var markets = await _broker.GetMarketsAsync(
            baseAssetFilter,
            cancellationToken);

        markets.CopyDictOfDictTo(_cachedMarkets);

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

    private async Task<IReadOnlyDictionary<string, Weight>> UpdateWeights(CancellationToken cancellationToken)
    {
        var weights = await _weightsService.GetWeightsAsync(cancellationToken);
        _cachedWeights.Clear();
        weights.CopyDictTo(_cachedWeights);
        return weights;
    }

    private IEnumerable<IGrouping<string, Trade>> CalculateTrades(IDictionary<string, Weight> weights)
    {
        var groups = _tradeFactory
            .CalculateTrades(weights, _cachedPositions, _cachedMarkets)
            .OrderBy(g => g.Key)
            .ToArray();

        foreach (var g in groups)
        {
            var tokenTradeResults = new ConcurrentDictionary<string, TradeResult>();

            foreach (var trade in g)
            {
                tokenTradeResults[trade.AssetName] = new TradeResult(trade);
            }

            _cachedTradeResults[g.Key] = tokenTradeResults;
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
            _tokenSubject.Dispose();
        }

        _disposed = true;
    }

    private void OnUpdate(MarketInfo marketInfo)
    {
        var token = marketInfo.BaseAsset;

        if (!_cachedMarkets.ContainsKey(token))
        {
            return;
        }

        var baseAssetMarkets = _cachedMarkets[token];
        baseAssetMarkets[marketInfo.Name] = marketInfo;
        _cachedMarkets[token] = baseAssetMarkets;

        if (_cachedTradeResults.TryGetValue(token, out var results) &&
            results.Values.All(tr => tr.Order?.OrderStatus is not OrderStatus.New or OrderStatus.Open))
        {
            RecalculateTrades(token);
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
        var token = position.BaseAsset;
        if (!_cachedPositions.ContainsKey(token))
            _cachedPositions[token] = new ConcurrentDictionary<string, Position>();
        var tokenPositions = _cachedPositions[token];
        tokenPositions[position.AssetName] = position;
        _cachedPositions[position.AssetName] = tokenPositions;
        OnUpdated(position.BaseAsset, positions: tokenPositions);
    }

    private void OnNext(TradeResult result)
    {
        var token = result.Trade.BaseAsset;
        var tokenTradeResults = _cachedTradeResults[token];
        tokenTradeResults[result.Trade.AssetName] = result;
        _cachedTradeResults[token] = tokenTradeResults;
        OnUpdated(token, tokenTradeResults);
    }

    private void OnUpdated(
        string baseAsset,
        IDictionary<string, TradeResult>? tradeResults = null,
        IDictionary<string, Position>? positions = null)
    {
        tradeResults ??= _cachedTradeResults[baseAsset];
        positions ??= _cachedPositions[baseAsset];
        
        _tokenSubject.OnNext((baseAsset,
            (IReadOnlyDictionary<string, TradeResult>) tradeResults,
            (IReadOnlyDictionary<string, Position>) positions));
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