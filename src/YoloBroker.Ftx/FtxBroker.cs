using System.Collections.Concurrent;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using FTX.Net.Clients;
using FTX.Net.Enums;
using FTX.Net.Interfaces.Clients;
using FTX.Net.Objects;
using FTX.Net.Objects.Models;
using FTX.Net.Objects.Models.Socket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloBroker.Ftx.Config;
using YoloBroker.Ftx.Exceptions;
using YoloBroker.Ftx.Extensions;
using Order = YoloAbstractions.Order;
using OrderSide = FTX.Net.Enums.OrderSide;
using Position = YoloAbstractions.Position;
using Trade = YoloAbstractions.Trade;

namespace YoloBroker.Ftx;

public class FtxBroker : IYoloBroker
{
    private static readonly TimeSpan MinTradeSendInterval = TimeSpan.FromMilliseconds(100);
    private readonly AssetPermissions _assetPermissions;
    private readonly IFTXClient _ftxClient;
    private readonly IFTXSocketClient _ftxSocketClient;
    private readonly ILogger<FtxBroker> _logger;
    private readonly Subject<MarketInfo> _marketUpdatesSubject;
    private readonly ConcurrentDictionary<long, OrderUpdate> _orderUpdates;
    private readonly Subject<OrderUpdate> _orderUpdatesSubject;
    private readonly Subject<Position> _positionUpdatesSubject;
    private readonly bool? _postOnly;
    private readonly string? _quoteCurrency;
    private readonly ConcurrentDictionary<string, FTXSymbol> _subscribedSymbols;
    private bool _disposed;
    private bool _subscribedOrderUpdates;

    public FtxBroker(IConfiguration configuration, ILogger<FtxBroker> logger)
        : this(configuration.GetFtxConfig(), configuration.GetYoloConfig(), logger)
    {
    }

    public FtxBroker(FtxConfig ftxConfig, YoloConfig yoloConfig, ILogger<FtxBroker> logger)
        : this(new FTXClient(
                new FTXClientOptions
                {
                    ApiCredentials = new ApiCredentials(ftxConfig.ApiKey, ftxConfig.Secret),
                    ApiOptions = new RestApiClientOptions(ftxConfig.RestApi),
                    //                  LogLevel = LogLevel.Debug,
                    SubaccountName = ftxConfig.SubAccount
                }),
            new FTXSocketClient(
                new FTXSocketClientOptions
                {
                    ApiCredentials = new ApiCredentials(ftxConfig.ApiKey, ftxConfig.Secret),
                    AutoReconnect = true,
                    //                   LogLevel = LogLevel.Debug,
                    StreamOptions = new ApiClientOptions(ftxConfig.WebSocketApi),
                    SubaccountName = ftxConfig.SubAccount
                }),
            logger,
            yoloConfig.AssetPermissions,
            yoloConfig.BaseAsset,
            ftxConfig.PostOnly
        )
    {
    }

    public FtxBroker(
        IFTXClient ftxClient,
        IFTXSocketClient ftxSocketClient,
        ILogger<FtxBroker> logger,
        AssetPermissions assetPermissions,
        string? quoteCurrency,
        bool? postOnly = null)
    {
        _ftxClient = ftxClient;
        _ftxSocketClient = ftxSocketClient;
        _logger = logger;
        _assetPermissions = assetPermissions;
        _quoteCurrency = quoteCurrency;
        _postOnly = postOnly;
        _orderUpdates = new ConcurrentDictionary<long, OrderUpdate>();
        _subscribedSymbols = new ConcurrentDictionary<string, FTXSymbol>();
        _marketUpdatesSubject = new Subject<MarketInfo>();
        _orderUpdatesSubject = new Subject<OrderUpdate>();
        _positionUpdatesSubject = new Subject<Position>();
    }

    public IObservable<MarketInfo> MarketUpdates => _marketUpdatesSubject;
    public IObservable<OrderUpdate> OrderUpdates => _orderUpdatesSubject;
    public IObservable<Position> PositionUpdates => _positionUpdatesSubject;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async IAsyncEnumerable<TradeResult> PlaceTradesAsync(
        IEnumerable<Trade> trades,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        async Task<DateTime> Throttle(DateTime? dateTime)
        {
            // throttle as FTX errors if > 2 orders in 200ms
            var timeSinceLastTrade = DateTime.UtcNow - dateTime;
            if (timeSinceLastTrade < MinTradeSendInterval)
                await Task.Delay(MinTradeSendInterval - timeSinceLastTrade.Value, ct);

            return DateTime.UtcNow;
        }

        await SubscribeOrderUpdates();

        DateTime? lastTradeTime = null;

        foreach (var trade in trades)
        {
            var orderSide = trade.Amount < 0 ? OrderSide.Sell : OrderSide.Buy;
            var orderType = trade.LimitPrice.HasValue ? OrderType.Limit : OrderType.Market;
            var quantity = Math.Abs(trade.Amount);

            lastTradeTime = await Throttle(lastTradeTime);

            var result = await _ftxClient.TradeApi.Trading.PlaceOrderAsync(
                trade.AssetName,
                orderSide,
                orderType,
                quantity,
                trade.LimitPrice,
                postOnly: _postOnly,
                ct: ct);

            LogData(result);

            var order = result.Data.ToOrder();
            var tradeResult = new TradeResult(
                trade,
                result.Success,
                order,
                result.Error?.Message,
                result.Error?.Code);

            yield return tradeResult;

            if (order is null)
                continue;

            OnNext(new OrderUpdate(trade, order));
        }
    }

    public async Task<Dictionary<long, Order>> GetOrdersAsync(CancellationToken ct)
    {
        var orders =
            await GetDataAsync(() => _ftxClient.TradeApi.Trading.GetOpenOrdersAsync(ct: ct),
                "Could not get open orders");

        return orders.ToDictionary(x => x.Id, x => x.ToOrder()!);
    }

    public async Task<IDictionary<string, IEnumerable<Position>>> GetPositionsAsync(
        CancellationToken ct = default)
    {
        var positions =
            await GetDataAsync(() => _ftxClient.TradeApi.Account.GetPositionsAsync(ct: ct),
                "Could not get account info");

        var result = new Dictionary<string, IEnumerable<Position>>();

        foreach (var position in positions)
        {
            var baseAsset = position.Future
                .Split("-")
                .First();

            result[baseAsset] = new List<Position>
            {
                new(
                    position.Future,
                    baseAsset,
                    AssetType.Future,
                    position.Quantity * (position.Side == OrderSide.Buy ? 1 : -1))
            };
        }

        var holdings = await GetDataAsync(
            () => _ftxClient.TradeApi.Account.GetBalancesAsync(ct: ct),
            "Could not get holdings");

        foreach (var holding in holdings)
        {
            var position = new Position(
                holding.Asset,
                holding.Asset,
                AssetType.Spot,
                holding.Total);

            if (result.TryGetValue(holding.Asset, out var positionsList))
                (positionsList as List<Position>)?.Add(position);
            else
                result[holding.Asset] = new List<Position>
                {
                    position
                };
        }

        return result;
    }

    public async Task<IDictionary<string, IEnumerable<MarketInfo>>> GetMarketsAsync(
        ISet<string>? baseAssetFilter = null,
        CancellationToken ct = default)
    {
        var symbols = await GetDataAsync(
            () => _ftxClient.TradeApi.ExchangeData.GetSymbolsAsync(ct),
            "Unable to get symbols"
        );

        bool Filter(FTXSymbol s)
        {
            if (_quoteCurrency is { } &&
                s.QuoteAsset is { } &&
                _quoteCurrency != s.QuoteAsset)
                return false;

            if (baseAssetFilter is { } &&
                !baseAssetFilter.Contains(s.BaseAsset ?? s.Underlying))
                return false;

            var expiry = s.GetExpiry();

            return s.Type switch
            {
                SymbolType.Future when expiry.HasValue => _assetPermissions.HasFlag(
                    AssetPermissions.ExpiringFutures),
                SymbolType.Future when !expiry.HasValue => _assetPermissions.HasFlag(
                    AssetPermissions.PerpetualFutures),
                SymbolType.Spot => _assetPermissions.HasFlag(AssetPermissions.LongSpot) ||
                                   _assetPermissions.HasFlag(AssetPermissions.ShortSpot),
                _ => throw new ArgumentOutOfRangeException(nameof(s.Type),
                    s.Type,
                    "unknown asset type")
            };
        }

        var filteredSymbols = symbols
            .Where(Filter)
            .ToArray();

        await SubscribeUpdates(filteredSymbols);

        return filteredSymbols
            .GroupBy(s => s.BaseAsset ?? s.Underlying)
            .ToDictionary(g => g.Key, g => g.Select(s => ToMarketInfo(s)));
    }

    public async Task CancelOrderAsync(long orderId, CancellationToken ct)
    {
        await _ftxClient.TradeApi.Trading.CancelOrderAsync(orderId, ct: ct);
    }

    private static MarketInfo ToMarketInfo(
        FTXSymbol s,
        decimal? ask = null,
        decimal? bid = null,
        decimal? last = null,
        DateTime? timeStamp = null) =>
        new(
            s.Name,
            s.BaseAsset ?? s.Underlying,
            s.QuoteAsset,
            s.Type.ToAssetType(),
            s.PriceStep,
            s.QuantityStep,
            s.MinProvideSize,
            ask ?? s.BestAskPrice,
            bid ?? s.BestBidPrice,
            last ?? s.LastPrice,
            s.GetExpiry(),
            timeStamp ?? DateTime.UtcNow);

    private async Task SubscribeUpdates(IEnumerable<FTXSymbol> symbols)
    {
        foreach (var symbol in symbols)
        {
            if (_subscribedSymbols.ContainsKey(symbol.Name))
                continue;

            _subscribedSymbols[symbol.Name] = symbol;
            await _ftxSocketClient.Streams.SubscribeToTickerUpdatesAsync(symbol.Name, OnNext);
        }
    }

    private void OnNext(DataEvent<FTXStreamTicker> e)
    {
        LogData(e.Data);

        if (e.Topic is null || !_subscribedSymbols.TryGetValue(e.Topic, out var symbol))
        {
            return;
        }

        _marketUpdatesSubject.OnNext(ToMarketInfo(symbol,
            e.Data.BestAskPrice,
            e.Data.BestBidPrice,
            e.Data.LastPrice,
            e.Timestamp));
    }

    private async Task SubscribeOrderUpdates()
    {
        if (!_subscribedOrderUpdates)
            await GetDataAsync(
                () => _ftxSocketClient.Streams.SubscribeToOrderUpdatesAsync(OnNext),
                "Unable to subscribe to order updates");
        _subscribedOrderUpdates = true;
    }

    private void OnNext(DataEvent<FTXOrder> e)
    {
        LogData(e.Data);

        if (_orderUpdates.TryGetValue(e.Data.Id, out var orderUpdate))
            OnNext(new OrderUpdate(orderUpdate.Trade, e.Data.ToOrder()!));
    }

    private void OnNext(OrderUpdate orderUpdate)
    {
        _orderUpdates[orderUpdate.Order.Id] = orderUpdate;
        _orderUpdatesSubject.OnNext(orderUpdate);
    }

    protected void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _orderUpdatesSubject.Dispose();
            _marketUpdatesSubject.Dispose();
            _positionUpdatesSubject.Dispose();
            _ftxClient.Dispose();
            _ftxSocketClient.Dispose();
        }

        _disposed = true;
    }

    private async Task<T> GetDataAsync<T>(Func<Task<WebCallResult<T>>> webCallFunc, string exceptionMessage)
    {
        var result = await webCallFunc();

        if (!result.Success)
            throw new FtxException(exceptionMessage, result);

        LogData(result.Data);

        return result.Data;
    }

    private async Task GetDataAsync<T>(Func<Task<CallResult<T>>> callFunc, string exceptionMessage)
    {
        var result = await callFunc();

        if (!result.Success)
            throw new FtxException(exceptionMessage, result);

        LogData(result.Data);
    }

    private void LogData<T>(T data)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogTrace("Received: {Data}", JsonConvert.SerializeObject(data));
    }
}