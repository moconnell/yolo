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
    private readonly IDictionary<string, IEnumerable<MarketInfo>> _cachedMarkets;
    private readonly IDictionary<string, Order> _cachedOrders;
    private readonly IDictionary<string, IEnumerable<Position>> _cachedPositions;
    private readonly ConcurrentDictionary<string, Weight> _cachedWeights;
    private readonly ILogger<Runtime> _logger;
    private readonly IDisposable _marketUpdatesSubscription;
    private readonly IDisposable _orderUpdatesSubscription;
    private readonly IDisposable _positionUpdatesSubscription;
    private readonly ITradeFactory _tradeFactory;
    private readonly Subject<TradeResult> _tradeResultsSubject;
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
        _tradeResultsSubject = new Subject<TradeResult>();
        _weightsService = weightsService;
        _broker = broker;
        _marketUpdatesSubscription = _broker.MarketUpdates.Subscribe(OnUpdate);
        _orderUpdatesSubscription = _broker.OrderUpdates.SelectMany(OnUpdate).Subscribe();
        _positionUpdatesSubscription = _broker.PositionUpdates.Subscribe(OnUpdate);
        _tradeFactory = tradeFactory;
        _logger = logger;
        _cachedOrders = new ConcurrentDictionary<string, Order>();
        _cachedMarkets = new ConcurrentDictionary<string, IEnumerable<MarketInfo>>();
        _cachedPositions = new ConcurrentDictionary<string, IEnumerable<Position>>();
        _cachedWeights = new ConcurrentDictionary<string, Weight>();

        UnfilledOrderTimeout = config.UnfilledOrderTimeout;
    }

    private bool AllFilled => _cachedOrders.Values.All(order => order.OrderStatus == OrderStatus.Filled);

    private IEnumerable<Order> UnfilledOrders =>
        _cachedOrders.Values.Where(order => order.OrderStatus != OrderStatus.Filled);

    public TimeSpan? UnfilledOrderTimeout { get; set; }

    public IObservable<TradeResult> TradeUpdates => _tradeResultsSubject;

    public async Task<IEnumerable<Trade>> RebalanceAsync(CancellationToken cancellationToken)
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
            do
            {
                await Task.Delay(UnfilledOrderTimeout.Value, cancellationToken);
                if (AllFilled)
                    return;

                await CancelOrdersAsync(UnfilledOrders);
                await UpdateWeights(cancellationToken);
                var newTrades = CalculateTrades(_cachedWeights);
                await PlaceTradesImplAsync(newTrades, cancellationToken);
            } while (!AllFilled);
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

    private IEnumerable<Trade> CalculateTrades(IDictionary<string, Weight> weights)
    {
        var trades = _tradeFactory
            .CalculateTrades(weights, _cachedPositions, _cachedMarkets)
            .OrderBy(trade => trade.AssetName)
            .ToArray();

        foreach (var trade in trades)
        {
            OnNext(new TradeResult(trade));
        }

        return trades;
    }

    private async Task PlaceTradesImplAsync(IEnumerable<Trade> trades, CancellationToken cancellationToken)
    {
        await foreach (var result in _broker.PlaceTradesAsync(trades, cancellationToken))
        {
            if (result.Order is { })
            {
                _cachedOrders[result.Trade.BaseAsset] = result.Order;
            }

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
            _tradeResultsSubject.Dispose();
        }

        _disposed = true;
    }

    private void OnNext(TradeResult result) => _tradeResultsSubject.OnNext(result);

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
    }

    private async Task<Unit> OnUpdate(OrderUpdate update)
    {
        var (trade, order) = update;

        _cachedOrders[trade.BaseAsset] = order;

        _tradeResultsSubject.OnNext(new TradeResult(trade, true, order));

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

    private async Task Resubmit(Trade trade)
    {
        var baseAsset = trade.BaseAsset;
        var weight = _cachedWeights[baseAsset];
        var newTrades = CalculateTrades(new Dictionary<string, Weight> { { baseAsset, weight } });
        await PlaceTradesAsync(newTrades, _cancellationToken);
    }
}