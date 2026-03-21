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
using Microsoft.Extensions.Options;
using Nethereum.Util;
using YoloAbstractions;
using YoloBroker.Hyperliquid.Config;
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
    private readonly string? _address;
    private readonly string? _vaultAddress;
    private readonly ILogger<HyperliquidBroker> _logger;

    public HyperliquidBroker(
        IHyperLiquidRestClient hyperliquidClient,
        IHyperLiquidSocketClient hyperliquidSocketClient,
        ITickerAliasService tickerAliasService,
        IOptions<HyperliquidConfig> options,
        ILogger<HyperliquidBroker> logger) : this(
            hyperliquidClient,
            hyperliquidSocketClient,
            tickerAliasService,
            options.Value,
            logger)
    {
    }

    public HyperliquidBroker(
        IHyperLiquidRestClient hyperliquidClient,
        IHyperLiquidSocketClient hyperliquidSocketClient,
        ITickerAliasService tickerAliasService,
        HyperliquidConfig config,
        ILogger<HyperliquidBroker> logger) : this(
            hyperliquidClient,
            hyperliquidSocketClient,
            tickerAliasService,
            config.Address,
            config.VaultAddress,
            logger)
    {
    }

    public HyperliquidBroker(
        IHyperLiquidRestClient hyperliquidClient,
        IHyperLiquidSocketClient hyperliquidSocketClient,
        ITickerAliasService tickerAliasService,
        string? vaultAddress,
        ILogger<HyperliquidBroker> logger) : this(
            hyperliquidClient,
            hyperliquidSocketClient,
            tickerAliasService,
            null,
            vaultAddress,
            logger)
    {
    }

    public HyperliquidBroker(
        IHyperLiquidRestClient hyperliquidClient,
        IHyperLiquidSocketClient hyperliquidSocketClient,
        ITickerAliasService tickerAliasService,
        string? address,
        string? vaultAddress,
        ILogger<HyperliquidBroker> logger)
    {
        _hyperliquidClient = hyperliquidClient;
        _hyperliquidSocketClient = hyperliquidSocketClient;
        _tickerAliasService = tickerAliasService;
        _address = address.IsValidEthereumAddressHexFormat() && !address.IsAnEmptyAddress()
            ? address
            : null;
        _vaultAddress = vaultAddress.IsValidEthereumAddressHexFormat() && !vaultAddress.IsAnEmptyAddress()
            ? vaultAddress
            : null;
        _logger = logger;
        _logger.LogInformation(
            "Initialized HyperliquidBroker with address: {Address}, vaultAddress: {VaultAddress}, net",
            _address ?? "null",
            _vaultAddress ?? "null");
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

    public BrokerAccountContext GetAccountContext() => new(_address, _vaultAddress);

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

        _logger.LogInformation("Placing {SpotCount} spot trades and {FuturesCount} futures trades", spotTrades.Count, futuresTrades.Count);

        var spotTask = spotTrades.Count != 0
            ? PlaceSpotOrdersAsync(spotTrades, ct)
            : Task.FromResult(new WebCallResultWrapper<IReadOnlyList<OrderResult>>(true, null, null, []));

        var futuresTask = futuresTrades.Count != 0
            ? PlaceFuturesOrdersAsync(futuresTrades, ct)
            : Task.FromResult(new WebCallResultWrapper<IReadOnlyList<OrderResult>>(true, null, null, []));

        await Task.WhenAll(spotTask, futuresTask);

        var spotResult = spotTask.Result;
        _logger.LogInformation("Processing spot order results for {Count} trades", spotResult.OrderResult.Count);
        for (var i = 0; i < spotResult.OrderResult.Count; i++)
        {
            var or = spotResult.OrderResult[i];
            var t = spotTrades[i];

            var orderSuccess = spotResult.Success && or.Success;

            yield return orderSuccess
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
                    or.ErrorMessage ?? spotResult.Error?.Message,
                    or.ErrorCode ?? spotResult.Error?.Code);
        }

        if (futuresTrades.Count == 0)
        {
            yield break;
        }

        var futuresResult = futuresTask.Result;
        _logger.LogInformation("Processing futures order results for {Count} trades", futuresResult.OrderResult.Count);
        for (var i = 0; i < futuresResult.OrderResult.Count; i++)
        {
            var or = futuresResult.OrderResult[i];
            var t = futuresTrades[i];

            var orderSuccess = futuresResult.Success && or.Success;

            yield return orderSuccess
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
                    or.ErrorMessage ?? futuresResult.Error?.Message,
                    or.ErrorCode ?? futuresResult.Error?.Code);
        }
    }

    public IAsyncEnumerable<BrokerOrderEvent> SubscribeOrderUpdatesAsync(CancellationToken ct = default)
    {
        var tradeResultUpdateChannel = Channel.CreateUnbounded<BrokerOrderEvent>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
            });

        var subscriptionAddress = _vaultAddress ?? _address;

        _logger.LogInformation(
            "Subscribing to Hyperliquid order updates for address {Address}",
            subscriptionAddress ?? "(api credential default)");

        var subscriptionTask = CallAsync(
            () => _hyperliquidSocketClient.FuturesApi.SubscribeToOrderUpdatesAsync(
                subscriptionAddress,
                HandleOrderStatusUpdates,
                ct),
            "Could not subscribe to futures order updates");

        subscriptionTask.GetAwaiter().GetResult();

        _logger.LogInformation("Subscribed to Hyperliquid order updates");

        return tradeResultUpdateChannel.Reader.ReadAllAsync(ct);

        void HandleOrderStatusUpdates(DataEvent<HyperLiquidOrderStatus[]> e)
        {
            if (e.Data == null || e.Data.Length == 0)
            {
                _logger.LogWarning("Received null or empty order update: {Update}", e);
                return;
            }

            _logger.LogInformation(
                "Received Hyperliquid WS order status callback with {Count} update(s)",
                e.Data.Length);

            foreach (var update in e.Data)
            {
                HandleSingleOrderUpdate(update);
            }
        }

        void HandleSingleOrderUpdate(HyperLiquidOrderStatus update)
        {
            if (string.IsNullOrEmpty(update.Order.ClientOrderId) || string.IsNullOrEmpty(update.Order.Symbol))
            {
                _logger.LogWarning(
                    "Ignoring order update missing client order id or symbol. OrderId={OrderId}, Symbol={Symbol}, ClientOrderId={ClientOrderId}",
                    update.Order.OrderId,
                    update.Order.Symbol,
                    update.Order.ClientOrderId);
                return;
            }

            _logger.LogInformation(
                "Received Hyperliquid order update: ClientOrderId={ClientOrderId}, OrderId={OrderId}, Symbol={Symbol}, Status={Status}",
                update.Order.ClientOrderId,
                update.Order.OrderId,
                update.Order.Symbol,
                update.Status);

            var orderStatus = update.Status.ToYoloOrderStatus();
            var success = orderStatus switch
            {
                OrderStatus.Canceled or OrderStatus.MarginCanceled or OrderStatus.Rejected => false,
                _ => true
            };

            var filledQuantity = Math.Max(0m, update.Order.Quantity - update.Order.QuantityRemaining);

            var order = new Order(
                update.Order.OrderId,
                update.Order.Symbol,
                update.Order.SymbolType.ToYolo(),
                update.Order.Timestamp,
                update.Order.OrderSide.ToYolo(),
                orderStatus,
                update.Order.Quantity,
                filledQuantity,
                update.Order.Price,
                update.Order.ClientOrderId);

            var e = new BrokerOrderEvent(
                update.Order.ClientOrderId,
                order,
                success,
                success ? null : orderStatus.ToString());

            if (!tradeResultUpdateChannel.Writer.TryWrite(e))
            {
                _logger.LogWarning(
                    "Failed to write order update to channel for ClientOrderId={ClientOrderId}. Channel may be completed.",
                    update.Order.ClientOrderId);
            }
        }
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
                () => _hyperliquidClient.SpotApi.Trading.CancelOrderAsync(order.Symbol, order.Id, _vaultAddress, ct: ct),
                "Could not cancel spot order");
        }
        else if (order.AssetType == AssetType.Future)
        {
            _logger.LogInformation("Cancelling futures order {OrderId} for symbol {Symbol}", order.Id, order.Symbol);
            await CallAsync(
                () => _hyperliquidClient.FuturesApi.Trading.CancelOrderAsync(order.Symbol, order.Id, _vaultAddress, ct: ct),
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
                vaultAddress: _vaultAddress,
                ct: ct),
            $"Could not update order {order.Id} for symbol {order.Symbol}");
    }

    public async Task<IReadOnlyDictionary<long, Order>> GetOpenOrdersAsync(CancellationToken ct = default)
    {
        var orders =
            await GetDataAsync(
                () => _hyperliquidClient.FuturesApi.Trading.GetOpenOrdersExtendedAsync(_vaultAddress, ct: ct),
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

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<Position>>> GetPositionsAsync(CancellationToken ct = default)
    {
        var futuresAccount = await GetDataAsync(
            () => _hyperliquidClient.FuturesApi.Account.GetAccountInfoAsync(_vaultAddress, ct: ct),
            "Could not get futures balances");
        var spotBalances = await GetDataAsync(
            () => _hyperliquidClient.SpotApi.Account.GetBalancesAsync(_vaultAddress, ct: ct),
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
        HashSet<string>? baseAssetFilter,
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
        HashSet<string>? baseAssetFilter,
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
        var orderPrice = await ResolveOrderPriceAsync(trade, ct);

        var result = await _hyperliquidClient.FuturesApi.Trading.PlaceOrderAsync(
            trade.Symbol,
            trade.OrderSide.ToHyperLiquid(),
            trade.OrderType.ToHyperLiquid(),
            trade.AbsoluteAmount,
            orderPrice,
            clientOrderId: trade.ClientOrderId,
            reduceOnly: trade.ReduceOnly,
            vaultAddress: _vaultAddress,
            ct: ct);

        return Wrap(result);
    }

    private async Task<WebCallResultWrapper<OrderResult>> PlaceSpotOrderAsync(Trade trade, CancellationToken ct)
    {
        var orderPrice = await ResolveOrderPriceAsync(trade, ct);

        var result = await _hyperliquidClient.SpotApi.Trading.PlaceOrderAsync(
            trade.Symbol,
            trade.OrderSide.ToHyperLiquid(),
            trade.OrderType.ToHyperLiquid(),
            trade.AbsoluteAmount,
            orderPrice,
            clientOrderId: trade.ClientOrderId,
            reduceOnly: trade.ReduceOnly,
            vaultAddress: _vaultAddress,
            ct: ct);

        return Wrap(result);
    }

    private async Task<decimal> ResolveOrderPriceAsync(Trade trade, CancellationToken ct)
    {
        if (trade.LimitPrice.HasValue)
        {
            return trade.LimitPrice.Value;
        }

        if (trade.OrderType != OrderType.Market)
        {
            return trade.LimitPrice.GetValueOrDefault();
        }

        var orderBook = trade.AssetType switch
        {
            AssetType.Spot => await GetSpotOrderBookAsync(trade.Symbol, ct),
            AssetType.Future => await GetFuturesOrderBookAsync(trade.Symbol, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(trade.AssetType), trade.AssetType, "AssetType not supported")
        };

        var bestAsk = orderBook.Levels.Asks.ElementAtOrDefault(0)?.Price;
        var bestBid = orderBook.Levels.Bids.ElementAtOrDefault(0)?.Price;

        var price = trade.OrderSide switch
        {
            YoloAbstractions.OrderSide.Buy => bestAsk ?? bestBid,
            YoloAbstractions.OrderSide.Sell => bestBid ?? bestAsk,
            _ => bestAsk ?? bestBid
        };

        if (!price.HasValue)
        {
            throw new InvalidOperationException($"Could not determine market order price for {trade.Symbol}: order book has no bid/ask");
        }

        return price.Value;
    }

    private async Task<WebCallResultWrapper<IReadOnlyList<OrderResult>>> PlaceSpotOrdersAsync(
        IEnumerable<Trade> trades,
        CancellationToken ct)
    {
        var orders = trades.Select(trade => new HyperLiquidOrderRequest(
                trade.Symbol,
                trade.OrderSide.ToHyperLiquid(),
                trade.OrderType.ToHyperLiquid(),
                trade.AbsoluteAmount,
                trade.LimitPrice.GetValueOrDefault(),
                clientOrderId: trade.ClientOrderId,
                reduceOnly: trade.ReduceOnly)).ToArray();

        _logger.LogDebug("Placing {Count} spot orders: {Orders}", orders.Length, orders);

        var result = await _hyperliquidClient.SpotApi.Trading.PlaceMultipleOrdersAsync(
            orders,
            vaultAddress: _vaultAddress,
            ct: ct);

        return Wrap(result);
    }

    private async Task<WebCallResultWrapper<IReadOnlyList<OrderResult>>> PlaceFuturesOrdersAsync(
        IEnumerable<Trade> trades,
        CancellationToken ct)
    {
        var orders = trades.Select(trade => new HyperLiquidOrderRequest(
                trade.Symbol,
                trade.OrderSide.ToHyperLiquid(),
                trade.OrderType.ToHyperLiquid(),
                trade.AbsoluteAmount,
                trade.LimitPrice.GetValueOrDefault(),
                clientOrderId: trade.ClientOrderId,
                reduceOnly: trade.ReduceOnly)).ToArray();

        _logger.LogInformation("Placing {Count} futures orders: {Orders}", orders.Length, orders);

        var result = await _hyperliquidClient.FuturesApi.Trading.PlaceMultipleOrdersAsync(orders, vaultAddress: _vaultAddress, ct: ct);

        _logger.LogInformation("PlaceMultipleOrdersAsync returned: Success={Success}, Error={Error}, DataNull={DataNull}, StatusCode={StatusCode}",
            result.Success, result.Error?.Message, result.Data is null, result.ResponseStatusCode);

        return Wrap(result);
    }

    private WebCallResultWrapper<OrderResult> Wrap(WebCallResult<HyperLiquidOrderResult> result)
    {
        _logger.LogDebug(
            "Wrapping result for single order with Success: {Success}, Error: {Error}, ResponseStatusCode: {ResponseStatusCode}, Data: {Data}",
            result.Success,
            result.Error,
            result.ResponseStatusCode,
            result.Data);

        return new(
            result.Success,
            result.Error,
            result.ResponseStatusCode,
            ToOrderResult(result.Success, result.Error, result.Data));
    }

    private WebCallResultWrapper<IReadOnlyList<OrderResult>> Wrap(WebCallResult<CallResult<HyperLiquidOrderResult>[]> result)
    {
        _logger.LogInformation(
            "Wrapping result for multiple orders with Success: {Success}, Error: {Error}, ResponseStatusCode: {ResponseStatusCode}, DataNull: {DataNull}, DataLength: {DataLength}",
            result.Success,
            result.Error,
            result.ResponseStatusCode,
            result.Data is null,
            result.Data?.Length ?? 0);

        // Fail fast if the API returned an error
        if (!result.Success)
        {
            var errorMessage = result.Error?.Message ?? $"PlaceMultipleOrdersAsync failed with status code {result.ResponseStatusCode}";
            _logger.LogError("PlaceMultipleOrdersAsync returned error: {Error}", errorMessage);
            throw new HyperliquidException(errorMessage, result);
        }

        if (result.Data is null)
        {
            _logger.LogError("PlaceMultipleOrdersAsync returned null Data despite Success=true");
            throw new HyperliquidException("PlaceMultipleOrdersAsync returned null Data despite Success=true", result);
        }

        if (result.Data.Length == 0)
        {
            _logger.LogWarning("PlaceMultipleOrdersAsync returned empty Data array");
            return new(true, null, result.ResponseStatusCode, []);
        }

        var orderResults = result.Data.Select(x => ToOrderResult(x?.Success ?? false, x?.Error, x?.Data)).ToList();
        _logger.LogInformation("Successfully wrapped {Count} order results from PlaceMultipleOrdersAsync", orderResults.Count);

        return new(
            true,
            null,
            result.ResponseStatusCode,
            orderResults);
    }

    private static OrderResult ToOrderResult(bool success, Error? error, HyperLiquidOrderResult? data) =>
        new(
            success,
            error?.Message,
            error?.Code,
            data?.OrderId,
            data?.Status.ToYoloOrderStatus() ?? OrderStatus.NotSet,
            data?.AveragePrice,
            data?.FilledQuantity);
}