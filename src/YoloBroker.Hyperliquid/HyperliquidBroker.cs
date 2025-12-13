using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using HyperLiquid.Net;
using HyperLiquid.Net.Enums;
using HyperLiquid.Net.Interfaces.Clients;
using HyperLiquid.Net.Objects.Models;
using Microsoft.Extensions.Logging;
using YoloAbstractions;
using YoloBroker.Hyperliquid.CustomSigning;
using YoloBroker.Hyperliquid.Exceptions;
using YoloBroker.Hyperliquid.Extensions;
using YoloBroker.Interface;
using OrderStatus = YoloAbstractions.OrderStatus;
using OrderType = YoloAbstractions.OrderType;

namespace YoloBroker.Hyperliquid;

public sealed class HyperliquidBroker : IYoloBroker
{
    static HyperliquidBroker()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            HyperLiquidExchange.SignRequestDelegate = NethereumSigningExtensions.SignMessage;
        }
    }

    private bool _disposed;
    private readonly IHyperLiquidRestClient _hyperliquidClient;
    private readonly IHyperLiquidSocketClient _hyperliquidSocketClient;
    private readonly ITickerAliasService _tickerAliasService;
    private readonly ILogger<HyperliquidBroker> _logger;

    public HyperliquidBroker(
        IHyperLiquidRestClient hyperliquidClient,
        IHyperLiquidSocketClient hyperliquidSocketClient,
        ITickerAliasService tickerAliasService,
        ILogger<HyperliquidBroker> logger)
    {
        _hyperliquidClient = hyperliquidClient;
        _hyperliquidSocketClient = hyperliquidSocketClient;
        _tickerAliasService = tickerAliasService;
        _logger = logger;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~HyperliquidBroker()
    {
        Dispose(false);
    }

    public void ConfigureSigning(Func<string, string, Dictionary<string, object>> requestSigningFunction)
    {
        HyperLiquidExchange.SignRequestDelegate = requestSigningFunction;
    }

    public async Task<TradeResult> PlaceTradeAsync(Trade trade, CancellationToken ct = default)
    {
        var result = trade switch
        {
            { AssetType: AssetType.Spot } => await PlaceSpotOrderAsync(trade, ct),
            { AssetType: AssetType.Future } => await PlaceFuturesOrderAsync(trade, ct),
            _ => throw new ArgumentOutOfRangeException(
                nameof(trade.AssetType),
                trade.AssetType,
                "AssetType not supported")
        };

        return new TradeResult(
            trade,
            result.Success,
            result.OrderResult.OrderId.HasValue
                ? new Order(
                    result.OrderResult.OrderId.Value,
                    trade.Symbol,
                    trade.AssetType,
                    DateTime.UtcNow,
                    trade.OrderSide,
                    result.OrderResult.OrderStatus,
                    trade.AbsoluteAmount,
                    result.OrderResult.FilledQuantity,
                    trade.LimitPrice,
                    trade.ClientOrderId)
                : null,
            result.Error?.Message,
            result.Error?.Code);
    }

    public async IAsyncEnumerable<TradeResult> PlaceTradesAsync(
        IEnumerable<Trade> trades,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trades);

        var tradeArray = trades as Trade[] ?? [.. trades];
        if (tradeArray.Length == 0)
        {
            yield break;
        }

        var spotTrades = tradeArray.Where(t => t.AssetType == AssetType.Spot).ToList();
        var futuresTrades = tradeArray.Where(t => t.AssetType == AssetType.Future).ToList();

        var spotTask = spotTrades.Count != 0
            ? PlaceSpotOrdersAsync(spotTrades, ct)
            : Task.FromResult(new WebCallResultWrapper<IReadOnlyList<OrderResult>>(true, null, null, []));

        var futuresTask = futuresTrades.Count != 0
            ? PlaceFuturesOrdersAsync(futuresTrades, ct)
            : Task.FromResult(new WebCallResultWrapper<IReadOnlyList<OrderResult>>(true, null, null, []));

        await Task.WhenAll(spotTask, futuresTask);

        var spotResult = spotTask.Result;
        for (var i = 0; i < spotResult.OrderResult.Count; i++)
        {
            var or = spotResult.OrderResult[i];
            var t = spotTrades[i];

            yield return spotResult.Success
                ? new TradeResult(
                    t,
                    true,
                    new Order(
                        or.OrderId.GetValueOrDefault(),
                        t.Symbol,
                        AssetType.Spot,
                        DateTime.UtcNow,
                        t.OrderSide,
                        or.OrderStatus,
                        t.AbsoluteAmount,
                        or.FilledQuantity,
                        t.LimitPrice,
                        t.ClientOrderId))
                : new TradeResult(
                    t,
                    false,
                    null,
                    spotResult.Error?.Message,
                    spotResult.Error?.Code);
        }

        if (futuresTrades.Count == 0)
        {
            yield break;
        }

        var futuresResult = futuresTask.Result;
        for (var i = 0; i < futuresResult.OrderResult.Count; i++)
        {
            var or = futuresResult.OrderResult[i];
            var t = futuresTrades[i];

            yield return futuresResult.Success
                ? new TradeResult(
                    t,
                    true,
                    new Order(
                        or.OrderId.GetValueOrDefault(),
                        t.Symbol,
                        AssetType.Future,
                        DateTime.UtcNow,
                        t.OrderSide,
                        or.OrderStatus,
                        t.AbsoluteAmount,
                        or.FilledQuantity,
                        t.LimitPrice,
                        t.ClientOrderId))
                : new TradeResult(
                    t,
                    false,
                    null,
                    futuresResult.Error?.Message,
                    futuresResult.Error?.Code);
        }
    }

    public async Task<IReadOnlyList<decimal>> GetDailyClosePricesAsync(
        string ticker,
        int periods,
        bool includeToday = false,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ticker, nameof(ticker));
        var klines = await GetDailyPriceHistoryAsync(ticker, periods, includeToday, ct);
        return [.. klines.Select(x => x.ClosePrice)];
    }

    public async IAsyncEnumerable<OrderUpdate> ManageOrdersAsync(
        IEnumerable<Trade> trades,
        OrderManagementSettings settings,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(settings);

        var tradeArray = trades as Trade[] ?? [.. trades];
        if (tradeArray.Length == 0)
        {
            yield break;
        }

        var updateChannel = Channel.CreateUnbounded<OrderUpdate>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });

        var orderTrackers = new ConcurrentDictionary<long, OrderTracker>();

        var spotOrderUpdatesSub = await CallAsync(
            () => _hyperliquidSocketClient.SpotApi.SubscribeToOrderUpdatesAsync(null, HandleOrderStatusUpdates, ct),
            "Could not subscribe to spot order updates");

        var futuresOrderUpdatesSub = await CallAsync(
            () => _hyperliquidSocketClient.FuturesApi.SubscribeToOrderUpdatesAsync(null, HandleOrderStatusUpdates, ct),
            "Could not subscribe to futures order updates");

        _logger.LogDebug("Subscribed to spot and futures order & trade updates");

        try
        {
            // Place initial limit orders
            await foreach (var result in PlaceTradesAsync(tradeArray, ct))
            {
                AddOrderTracker(result);
                var update = ToOrderUpdate(result);
                _logger.LogDebug("Returning OrderUpdate {OrderUpdate}", update);
                yield return update;
            }

            // Start timeout checking task
            var timeoutTask = StartTimeoutMonitoringTask(settings, updateChannel, orderTrackers, ct);

            await foreach (var update in updateChannel.Reader.ReadAllAsync(ct))
            {
                _logger.LogDebug("Returning OrderUpdate {OrderUpdate}", update);
                yield return update;
            }

            await timeoutTask;
        }
        finally
        {
            updateChannel.Writer.TryComplete();
            await spotOrderUpdatesSub.CloseAsync();
            await futuresOrderUpdatesSub.CloseAsync();
            _logger.LogDebug("Unsubscribed from spot and futures order updates");
        }

        yield break;

        void HandleOrderStatusUpdates(DataEvent<HyperLiquidOrderStatus[]> e)
        {
            if (e.Data == null || e.Data.Length == 0)
            {
                _logger.LogWarning("Received null or empty order update: {Update}", e);
                return;
            }

            foreach (var update in e.Data)
            {
                if (!orderTrackers.TryGetValue(update.Order.OrderId, out var tracker))
                {
                    _logger.LogWarning(
                        "Received order update for unknown order {OrderId}: {Update}",
                        update.Order.OrderId,
                        update);
                    continue;
                }

                var orderStatus = update.Status.ToYoloOrderStatus();
                var newOrder = tracker.Order with
                {
                    Filled = tracker.Order.Amount - update.Order.QuantityRemaining,
                    OrderStatus = orderStatus
                };

                orderTrackers[tracker.Order.Id] = tracker with
                {
                    Order = newOrder
                };

                var message = update.Status.ToString();

                switch (orderStatus)
                {
                    case OrderStatus.Filled:
                        updateChannel.Writer.TryWrite(
                            new OrderUpdate(newOrder.Symbol, OrderUpdateType.Filled, newOrder, Message: message));
                        RemoveOrderTracker(tracker.MarkComplete().Order.Id);
                        break;

                    case OrderStatus.Canceled:
                    case OrderStatus.MarginCanceled:
                    case OrderStatus.Rejected:
                        updateChannel.Writer.TryWrite(
                            new OrderUpdate(newOrder.Symbol, OrderUpdateType.Cancelled, newOrder, Message: message));
                        RemoveOrderTracker(tracker.MarkComplete().Order.Id);
                        break;

                    default:
                        var orderUpdateType =
                            (update.Order.QuantityRemaining > 0 &&
                             update.Order.QuantityRemaining < tracker.Order.Amount)
                                ? OrderUpdateType.PartiallyFilled
                                : OrderUpdateType.Created;
                        updateChannel.Writer.TryWrite(
                            new OrderUpdate(tracker.Order.Symbol, orderUpdateType, newOrder, Message: message));
                        break;
                }
            }

            if (orderTrackers.IsEmpty)
            {
                updateChannel.Writer.TryComplete();
            }
        }

        OrderUpdate ToOrderUpdate(TradeResult result)
        {
            return new OrderUpdate(
                result.Trade.Symbol,
                GetOrderUpdateType(),
                result.Order,
                Error: result.Success
                    ? null
                    : new HyperliquidException(result.Error!, result.ErrorCode.GetValueOrDefault()));

            OrderUpdateType GetOrderUpdateType()
            {
                if (!result.Success || result.Order == null)
                {
                    return OrderUpdateType.Error;
                }

                if (result.Trade.OrderType == OrderType.Market)
                {
                    return OrderUpdateType.MarketOrderPlaced;
                }

                var order = result.Order;
                return order.OrderStatus switch
                {
                    OrderStatus.Canceled or OrderStatus.MarginCanceled => OrderUpdateType.Cancelled,
                    OrderStatus.Filled => OrderUpdateType.Filled,
                    OrderStatus.Rejected => OrderUpdateType.Error,
                    _ when order.Filled > 0 && order.Filled < order.Amount => OrderUpdateType.PartiallyFilled,
                    _ => OrderUpdateType.Created
                };
            }
        }

        void AddOrderTracker(TradeResult result)
        {
            if (!result.Success || result.Order == null || result.Order.IsCompleted())
            {
                return;
            }

            var orderId = result.Order.Id;
            var order = result.Order;
            var trade = result.Trade;

            if (orderTrackers.TryAdd(orderId, new OrderTracker(order, trade, DateTime.UtcNow)))
            {
                _logger.LogDebug("Added order tracker for order {OrderId} for {Symbol}", orderId, order.Symbol);
            }
        }

        void RemoveOrderTracker(long orderId)
        {
            if (orderTrackers.TryRemove(orderId, out _))
            {
                _logger.LogDebug("Removed order tracker for order {OrderId}", orderId);
            }
        }
    }

    private Task StartTimeoutMonitoringTask(
        OrderManagementSettings settings,
        Channel<OrderUpdate> updateChannel,
        ConcurrentDictionary<long, OrderTracker> orderTrackers,
        CancellationToken ct)
    {
        return Task.Run(
            async () =>
            {
                try
                {
                    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

                    while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
                    {
                        var now = DateTime.UtcNow;
                        var timedOutTrackers = orderTrackers.Values
                            .Where(t => now - t.CreatedAt > settings.UnfilledOrderTimeout && !t.IsComplete)
                            .ToList();

                        foreach (var tracker in timedOutTrackers)
                        {
                            _logger.LogInformation(
                                "Order {OrderId} for {AssetName} timed out and will be cancelled",
                                tracker.Order.Id,
                                tracker.Order.Symbol);

                            try
                            {
                                await CancelOrderAsync(tracker.Order, ct);
                                updateChannel.Writer.TryWrite(
                                    new OrderUpdate(
                                        tracker.Order.Symbol,
                                        OrderUpdateType.TimedOut,
                                        tracker.Order with { OrderStatus = OrderStatus.Canceled },
                                        Message: "Order timed out"));
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(
                                    ex,
                                    $"Failed to cancel order {{OrderId}} for {{AssetName}}: {{ErrorMessage}}",
                                    tracker.Order.Id,
                                    tracker.Order.Symbol,
                                    ex.Message);
                            }

                            if (settings.SwitchToMarketOnTimeout)
                            {
                                _logger.LogInformation("Creating market order for {AssetName}", tracker.Order.Symbol);

                                try
                                {
                                    var marketTrade = tracker.OriginalTrade with { OrderType = OrderType.Market };
                                    var marketTradeResult = await PlaceTradeAsync(marketTrade, ct);
                                    if (marketTradeResult.Success)
                                    {
                                        updateChannel.Writer.TryWrite(
                                            new OrderUpdate(
                                                marketTrade.Symbol,
                                                OrderUpdateType.MarketOrderPlaced,
                                                marketTradeResult.Order));
                                    }
                                    else
                                    {
                                        updateChannel.Writer.TryWrite(
                                            new OrderUpdate(
                                                tracker.Order.Symbol,
                                                OrderUpdateType.Error,
                                                Message: $"Failed to create market order: {marketTradeResult.Error}"));
                                    }
                                }
                                catch (Exception ex)
                                {
                                    updateChannel.Writer.TryWrite(
                                        new OrderUpdate(
                                            tracker.Order.Symbol,
                                            OrderUpdateType.Error,
                                            Message: $"Failed to create market order: {ex.Message}",
                                            Error: ex));
                                }
                            }

                            tracker.MarkComplete();
                            orderTrackers.TryRemove(tracker.Order.Id, out _);
                        }

                        // Check if all orders are complete after timeout processing
                        if (orderTrackers.IsEmpty)
                        {
                            _logger.LogInformation("All orders completed after timeout processing, completing channel");
                            updateChannel.Writer.TryComplete();
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in timeout monitoring task");
                    updateChannel.Writer.TryComplete(ex);
                }
                finally
                {
                    updateChannel.Writer.TryComplete();
                }
            },
            ct);
    }

    public async Task CancelOrderAsync(Order order, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        _logger.LogInformation("Cancelling order {Order}", order);

        if (order.IsCompleted())
        {
            _logger.LogWarning("Order {OrderId} is already completed, skipping cancellation", order.Id);
            return;
        }

        if (order.AssetType == AssetType.Spot)
        {
            _logger.LogInformation("Cancelling spot order {OrderId} for symbol {Symbol}", order.Id, order.Symbol);
            await CallAsync(
                () => _hyperliquidClient.SpotApi.Trading.CancelOrderAsync(order.Symbol, order.Id, ct: ct),
                "Could not cancel spot order");
        }
        else if (order.AssetType == AssetType.Future)
        {
            _logger.LogInformation("Cancelling futures order {OrderId} for symbol {Symbol}", order.Id, order.Symbol);
            await CallAsync(
                () => _hyperliquidClient.FuturesApi.Trading.CancelOrderAsync(order.Symbol, order.Id, ct: ct),
                "Could not cancel order");
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(order.AssetType), order.AssetType, "AssetType not supported");
        }
    }

    public async Task EditOrderAsync(Order order, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        _logger.LogInformation("Updating order {OrderId} for symbol {Symbol}", order.Id, order.Symbol);

        await CallAsync(
            () => _hyperliquidClient.FuturesApi.Trading.EditOrderAsync(
                order.Symbol,
                order.Id,
                order.ClientId,
                order.OrderSide.ToHyperLiquid(),
                order.OrderType.ToHyperLiquid(),
                order.Amount,
                order.LimitPrice.GetValueOrDefault(),
                ct: ct),
            $"Could not update order {order.Id} for symbol {order.Symbol}");
    }

    public async Task<IReadOnlyDictionary<long, Order>> GetOpenOrdersAsync(CancellationToken ct = default)
    {
        var orders =
            await GetDataAsync(
                () => _hyperliquidClient.FuturesApi.Trading.GetOpenOrdersExtendedAsync(ct: ct),
                "Could not get orders");

        return orders.ToDictionary(
            o => o.OrderId,
            o => new Order(
                o.OrderId,
                o.Symbol!,
                o.SymbolType.ToYolo(),
                o.Timestamp,
                o.OrderSide.ToYolo(),
                OrderStatus.Open,
                o.Quantity,
                o.QuantityRemaining,
                o.Price,
                o.ClientOrderId));
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<Position>>> GetPositionsAsync(
        CancellationToken ct = default)
    {
        var futuresAccount = await GetDataAsync(
            () => _hyperliquidClient.FuturesApi.Account.GetAccountInfoAsync(ct: ct),
            "Could not get futures balances");
        var spotBalances = await GetDataAsync(
            () => _hyperliquidClient.SpotApi.Account.GetBalancesAsync(ct: ct),
            "Could not get spot balances");

        var futuresPositions = GetPositionsFromPositions(futuresAccount.Positions);
        var spotPositions = GetPositionsFromBalances(
            spotBalances
                .Where(x => x.Asset != "USDC" && x.Total != 0));

        var result = new Dictionary<string, IReadOnlyList<Position>>(futuresPositions);
        foreach (var (key, value) in spotPositions)
        {
            if (result.TryGetValue(key, out var existingPositions))
            {
                result[key] = [.. existingPositions, .. value];
            }
            else
            {
                result[key] = value;
            }
        }

        return result;

        Dictionary<string, IReadOnlyList<Position>> GetPositionsFromBalances(
            IEnumerable<HyperLiquidBalance> balances,
            AssetType assetType = AssetType.Spot)
        {
            return balances.GroupBy(x => x.Asset)
                .ToDictionary(
                    g => g.Key,
                    IReadOnlyList<Position> (g) =>
                        [.. g.Select(x => new Position(x.Asset, x.Asset, assetType, x.Total))]);
        }

        Dictionary<string, IReadOnlyList<Position>> GetPositionsFromPositions(
            IEnumerable<HyperLiquidPosition> positions,
            AssetType assetType = AssetType.Future)
        {
            return positions.GroupBy(x => x.Position.Symbol)
                .ToDictionary(
                    g => _tickerAliasService.TryGetTicker(g.Key, out var ticker) ? ticker! : g.Key,
                    IReadOnlyList<Position> (g) =>
                    [
                        .. g.Select(x => new Position(
                            x.Position.Symbol,
                            x.Position.Symbol,
                            assetType,
                            x.Position.PositionQuantity.GetValueOrDefault()))
                    ]);
        }
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>>> GetMarketsAsync(
        ISet<string>? baseAssetFilter = null,
        string? quoteCurrency = null,
        AssetPermissions assetPermissions = AssetPermissions.All,
        CancellationToken ct = default)
    {
        var effectiveQuoteCurrency = string.IsNullOrEmpty(quoteCurrency) ? "USDC" : quoteCurrency;
        var aliasedBaseAssetFilter = baseAssetFilter?
            .Select(ticker => _tickerAliasService.TryGetAlias(ticker, out var alias) ? alias! : ticker)
            .ToHashSet();

        _logger.LogDebug(
            "Getting markets with baseAssetFilter: {BaseAssetFilter}, quoteCurrency: {QuoteCurrency}, assetPermissions: {AssetPermissions}",
            aliasedBaseAssetFilter,
            effectiveQuoteCurrency,
            assetPermissions);

        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>();

        if (assetPermissions.HasFlag(AssetPermissions.PerpetualFutures) ||
            assetPermissions.HasFlag(AssetPermissions.LongSpotAndPerp) ||
            assetPermissions.HasFlag(AssetPermissions.SpotAndPerp))
        {
            markets = await GetFuturesMarketsAsync(
                aliasedBaseAssetFilter,
                effectiveQuoteCurrency,
                ct);
        }

        if (assetPermissions.HasFlag(AssetPermissions.Spot) ||
            assetPermissions.HasFlag(AssetPermissions.LongSpot) ||
            assetPermissions.HasFlag(AssetPermissions.ShortSpot) ||
            assetPermissions.HasFlag(AssetPermissions.SpotAndPerp))
        {
            var spotMarkets = await GetSpotMarketsAsync(
                aliasedBaseAssetFilter,
                effectiveQuoteCurrency,
                ct);
            foreach (var kvp in spotMarkets)
            {
                if (markets.TryGetValue(kvp.Key, out var value))
                {
                    markets[kvp.Key] = [.. value, .. kvp.Value];
                }
                else
                {
                    markets[kvp.Key] = kvp.Value;
                }
            }
        }

        return markets.Select(kvp =>
                _tickerAliasService.TryGetTicker(kvp.Key, out var ticker)
                    ? new KeyValuePair<string, IReadOnlyList<MarketInfo>>(ticker!, kvp.Value)
                    : kvp)
            .ToDictionary();
    }

    private async Task<Dictionary<string, IReadOnlyList<MarketInfo>>> GetSpotMarketsAsync(
        ISet<string>? baseAssetFilter,
        string quoteCurrency,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting spot markets with baseAssetFilter: {BaseAssetFilter}, quoteCurrency: {QuoteCurrency}",
            baseAssetFilter,
            quoteCurrency);

        var spotExchangeInfo = await GetDataAsync(
            () => _hyperliquidClient.SpotApi.ExchangeData.GetExchangeInfoAsync(ct: ct),
            "Could not get spot exchange info");

        var tickersAndPrices = await Task.WhenAll(
            spotExchangeInfo.Symbols
                .Where(symbol => (baseAssetFilter == null || baseAssetFilter.Contains(symbol.BaseAsset.Name)) &&
                                 (string.IsNullOrEmpty(quoteCurrency) || symbol.QuoteAsset.Name == quoteCurrency))
                .Select(async symbol => (symbol, await GetSpotOrderBookAsync(symbol.Name, ct))));

        return tickersAndPrices
            .ToDictionary<(HyperLiquidSymbol Symbol, HyperLiquidOrderBook OrderBook), string
                ,
                IReadOnlyList<MarketInfo>>(
                tuple => tuple.Symbol.Name,
                tuple => [ToMarketInfo(tuple.Symbol, tuple.OrderBook)]);
    }

    private static MarketInfo ToMarketInfo(
        HyperLiquidSymbol symbol,
        HyperLiquidOrderBook orderBook)
    {
        var priceStep = Convert.ToDecimal(Math.Pow(10, -6 + symbol.QuoteAsset.PriceDecimals));
        var quantityStep = Convert.ToDecimal(Math.Pow(10, -symbol.QuoteAsset.QuantityDecimals));
        var ask = orderBook.Levels.Asks.ElementAtOrDefault(0)?.Price;
        var bid = orderBook.Levels.Bids.ElementAtOrDefault(0)?.Price;
        var mid = (ask + bid) / 2;
        var minProvideSize = ask.HasValue
            ? Math.Ceiling(10 / ask.Value / quantityStep) * quantityStep
            : quantityStep;

        return new MarketInfo(
            Name: symbol.Name,
            BaseAsset: symbol.BaseAsset.Name,
            QuoteAsset: symbol.QuoteAsset.Name,
            AssetType: AssetType.Spot,
            TimeStamp: DateTime.UtcNow,
            PriceStep: priceStep,
            QuantityStep: quantityStep,
            MinProvideSize: minProvideSize,
            Ask: ask,
            Bid: bid,
            Mid: mid);
    }

    private async Task<HyperLiquidOrderBook> GetSpotOrderBookAsync(string symbol, CancellationToken ct)
    {
        var orderBook = await GetDataAsync(
            () => _hyperliquidClient.SpotApi.ExchangeData.GetOrderBookAsync(symbol, ct: ct),
            $"Could not get order book for {symbol}");

        return orderBook;
    }

    private async Task<Dictionary<string, IReadOnlyList<MarketInfo>>> GetFuturesMarketsAsync(
        ISet<string>? baseAssetFilter,
        string quoteCurrency,
        CancellationToken ct = default)
    {
        var futuresExchangeInfo = await GetDataAsync(
            () => _hyperliquidClient.FuturesApi.ExchangeData.GetExchangeInfoAndTickersAsync(ct: ct),
            "Could not get futures exchange info and tickers");

        var tickersAndPrices = await Task.WhenAll(
            futuresExchangeInfo.ExchangeInfo.Symbols
                .Where(symbol => baseAssetFilter == null || baseAssetFilter.Contains(symbol.Name))
                .Select(async symbol => (symbol, await GetFuturesOrderBookAsync(symbol.Name, ct))));

        _logger.LogDebug("Retrieved {Count} tickers and prices", tickersAndPrices.Length);

        return tickersAndPrices
            .ToDictionary<(HyperLiquidFuturesSymbol Symbol, HyperLiquidOrderBook OrderBook),
                string,
                IReadOnlyList<MarketInfo>>(
                tuple => tuple.Symbol.Name,
                tuple => [ToMarketInfo(tuple.Symbol, quoteCurrency, tuple.OrderBook)]);
    }

    private static MarketInfo ToMarketInfo(
        HyperLiquidFuturesSymbol symbol,
        string quoteCurrency,
        HyperLiquidOrderBook orderBook)
    {
        var baseTickSize = Convert.ToDecimal(Math.Pow(10, -6 + symbol.QuantityDecimals));
        var quantityStep = Convert.ToDecimal(Math.Pow(10, -symbol.QuantityDecimals));
        var priceStep = orderBook.Levels.Bids.ElementAtOrDefault(0)?.Price.CalculateValidTickSize(baseTickSize);
        var ask = orderBook.Levels.Asks.ElementAtOrDefault(0)?.Price;
        var bid = orderBook.Levels.Bids.ElementAtOrDefault(0)?.Price;
        var mid = (ask + bid) / 2;
        var minProvideSize = ask.HasValue
            ? Math.Ceiling(10 / ask.Value / quantityStep) * quantityStep
            : quantityStep;

        return new MarketInfo(
            Name: symbol.Name,
            BaseAsset: symbol.Name,
            QuoteAsset: quoteCurrency,
            AssetType: AssetType.Future,
            TimeStamp: DateTime.UtcNow,
            PriceStep: priceStep,
            QuantityStep: quantityStep,
            MinProvideSize: minProvideSize,
            Ask: ask,
            Bid: bid,
            Mid: mid);
    }

    private async Task<HyperLiquidOrderBook> GetFuturesOrderBookAsync(string symbol, CancellationToken ct)
    {
        var orderBook = await GetDataAsync(
            () => _hyperliquidClient.FuturesApi.ExchangeData.GetOrderBookAsync(symbol, ct: ct),
            $"Could not get order book for {symbol}");

        return orderBook;
    }

    private async Task<HyperLiquidKline[]> GetDailyPriceHistoryAsync(
        string ticker,
        int periods,
        bool includeToday = false,
        CancellationToken ct = default)
    {
        ticker = _tickerAliasService.TryGetAlias(ticker, out var alias) ? alias! : ticker;
        var endDate = includeToday ? DateTime.Today : DateTime.Today.AddDays(-1);
        var startDate = endDate.AddDays(-periods + 1);
        var klines = await GetDataAsync(
            () => _hyperliquidClient.SpotApi.ExchangeData.GetKlinesAsync(
                ticker,
                KlineInterval.OneDay,
                startDate,
                endDate,
                ct),
            $"Could not get prices for {ticker}");

        return klines;
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _hyperliquidClient.Dispose();
            _hyperliquidSocketClient.Dispose();
        }

        _disposed = true;
    }

    private static async Task CallAsync(Func<Task<WebCallResult>> webCallFunc, string exceptionMessage)
    {
        var result = await webCallFunc();

        if (!result.Success)
        {
            throw new HyperliquidException(exceptionMessage, result);
        }
    }

    private static async Task<T> CallAsync<T>(Func<Task<CallResult<T>>> webCallFunc, string exceptionMessage)
    {
        var result = await webCallFunc();

        if (!result.Success)
        {
            throw new HyperliquidException(exceptionMessage, result);
        }

        return result.Data;
    }

    private static async Task<T> GetDataAsync<T>(
        Func<Task<WebCallResult<T>>> webCallFunc,
        string exceptionMessage)
    {
        var result = await webCallFunc();

        if (!result.Success)
        {
            throw new HyperliquidException(exceptionMessage, result);
        }

        return result.Data;
    }

    private async Task<WebCallResultWrapper<OrderResult>> PlaceFuturesOrderAsync(Trade trade, CancellationToken ct)
    {
        var result = await _hyperliquidClient.FuturesApi.Trading.PlaceOrderAsync(
            trade.Symbol,
            trade.OrderSide.ToHyperLiquid(),
            trade.OrderType.ToHyperLiquid(),
            trade.AbsoluteAmount,
            trade.LimitPrice.GetValueOrDefault(),
            clientOrderId: trade.ClientOrderId,
            ct: ct);

        return Wrap(result);
    }

    private async Task<WebCallResultWrapper<OrderResult>> PlaceSpotOrderAsync(Trade trade, CancellationToken ct)
    {
        var result = await _hyperliquidClient.SpotApi.Trading.PlaceOrderAsync(
            trade.Symbol,
            trade.OrderSide.ToHyperLiquid(),
            trade.OrderType.ToHyperLiquid(),
            trade.AbsoluteAmount,
            trade.LimitPrice.GetValueOrDefault(),
            clientOrderId: trade.ClientOrderId,
            ct: ct);

        return Wrap(result);
    }

    private async Task<WebCallResultWrapper<IReadOnlyList<OrderResult>>> PlaceSpotOrdersAsync(
        IEnumerable<Trade> trades,
        CancellationToken ct)
    {
        var result = await _hyperliquidClient.SpotApi.Trading.PlaceMultipleOrdersAsync(
            trades.Select(trade => new HyperLiquidOrderRequest(
                trade.Symbol,
                trade.OrderSide.ToHyperLiquid(),
                trade.OrderType.ToHyperLiquid(),
                trade.AbsoluteAmount,
                trade.LimitPrice.GetValueOrDefault(),
                clientOrderId: trade.ClientOrderId)),
            ct: ct);

        return Wrap(result);
    }

    private async Task<WebCallResultWrapper<IReadOnlyList<OrderResult>>> PlaceFuturesOrdersAsync(
        IEnumerable<Trade> trades,
        CancellationToken ct)
    {
        var result = await _hyperliquidClient.FuturesApi.Trading.PlaceMultipleOrdersAsync(
            trades.Select(trade => new HyperLiquidOrderRequest(
                trade.Symbol,
                trade.OrderSide.ToHyperLiquid(),
                trade.OrderType.ToHyperLiquid(),
                trade.AbsoluteAmount,
                trade.LimitPrice.GetValueOrDefault(),
                clientOrderId: trade.ClientOrderId)),
            ct: ct);

        return Wrap(result);
    }

    private static WebCallResultWrapper<OrderResult> Wrap(WebCallResult<HyperLiquidOrderResult> result) =>
        new(
            result.Success,
            result.Error,
            result.ResponseStatusCode,
            ToOrderResult(result.Data));

    private static WebCallResultWrapper<IReadOnlyList<OrderResult>> Wrap(
        WebCallResult<CallResult<HyperLiquidOrderResult>[]> result) =>
        new(
            result.Success,
            result.Error,
            result.ResponseStatusCode,
            [
                .. result.Data
                    .Where(x => x.Success)
                    .Select(x => ToOrderResult(x.Data))
            ]);

    private static OrderResult ToOrderResult(HyperLiquidOrderResult? data) =>
        new(
            data?.OrderId,
            data?.Status.ToYoloOrderStatus() ?? OrderStatus.NotSet,
            data?.AveragePrice,
            data?.FilledQuantity);

    private record OrderTracker(Order Order, Trade OriginalTrade, DateTime CreatedAt)
    {
        public bool IsComplete { get; private set; }

        public OrderTracker MarkComplete()
        {
            IsComplete = true;
            return this;
        }
    }
}