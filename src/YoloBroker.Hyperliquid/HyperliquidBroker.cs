using CryptoExchange.Net.Objects;
using HyperLiquid.Net.Enums;
using HyperLiquid.Net.Objects.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HyperLiquid.Net;
using Microsoft.Extensions.Logging;
using YoloAbstractions;
using YoloBroker.Hyperliquid.Exceptions;
using YoloBroker.Hyperliquid.Extensions;
using YoloBroker.Interface;
using OrderSide = HyperLiquid.Net.Enums.OrderSide;
using OrderStatus = YoloAbstractions.OrderStatus;
using HyperLiquid.Net.Interfaces.Clients;
using YoloBroker.Hyperliquid.CustomSigning;

namespace YoloBroker.Hyperliquid;

public sealed class HyperliquidBroker : IYoloBroker
{
    private bool _disposed;
    private readonly IHyperLiquidRestClient _hyperliquidClient;
    private readonly IHyperLiquidSocketClient _hyperliquidSocketClient;
    private readonly ILogger<HyperliquidBroker> _logger;

    public HyperliquidBroker(IHyperLiquidRestClient hyperliquidClient,
        IHyperLiquidSocketClient hyperliquidSocketClient,
        ILogger<HyperliquidBroker> logger)
    {
        _hyperliquidClient = hyperliquidClient;
        _hyperliquidSocketClient = hyperliquidSocketClient;
        _logger = logger;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            HyperLiquidExchange.SignRequestDelegate = NethereumSigningExtensions.SignMessage;
        }
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
          

    public async IAsyncEnumerable<TradeResult> PlaceTradesAsync(
        IEnumerable<Trade> trades,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var trade in trades)
        {
            if (ct.IsCancellationRequested)
                yield break;

            var orderSide = trade.Amount < 0 ? OrderSide.Sell : OrderSide.Buy;
            var quantity = Math.Abs(trade.Amount);

            var result = trade switch
            {
                { AssetType: AssetType.Spot } => await PlaceSpotOrderAsync(
                    trade,
                    orderSide,
                    quantity,
                    trade.LimitPrice,
                    ct),
                { AssetType: AssetType.Future } => await PlaceFuturesOrderAsync(
                    trade,
                    orderSide,
                    quantity,
                    trade.LimitPrice,
                    ct),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(trade.AssetType),
                    trade.AssetType,
                    "AssetType not supported")
            };

            var yaSide = orderSide switch
            {
                OrderSide.Buy => YoloAbstractions.OrderSide.Buy,
                OrderSide.Sell => YoloAbstractions.OrderSide.Sell,
                _ => throw new ArgumentOutOfRangeException()
            };

            yield return new TradeResult(
                trade,
                result.Success,
                new Order(
                    result.OrderId,
                    trade.AssetName,
                    DateTime.UtcNow,
                    yaSide,
                    result.OrderStatus,
                    quantity,
                    result.FilledQuantity),
                result.Error?.Message,
                result.Error?.Code);
        }
    }

    public async Task<Dictionary<long, Order>> GetOrdersAsync(CancellationToken ct = default)
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
                o.Timestamp,
                o.OrderSide.ToYolo(),
                OrderStatus.Open,
                o.Quantity,
                o.QuantityRemaining,
                o.Price,
                o.ClientOrderId));

    }

    public async Task<IDictionary<string, IReadOnlyList<Position>>> GetPositionsAsync(CancellationToken ct = default)
    {
        var futuresAccount = await GetDataAsync(
            () => _hyperliquidClient.FuturesApi.Account.GetAccountInfoAsync(ct: ct),
            "Could not get futures balances");
        var spotBalances = await GetDataAsync(
            () => _hyperliquidClient.SpotApi.Account.GetBalancesAsync(ct: ct),
            "Could not get spot balances");

        var pos1 = GetPositionsFromPositions(futuresAccount.Positions);
        var pos2 = GetPositionsFromBalances(spotBalances);
        var posResult = pos1.Concat(pos2).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Debug.Assert(posResult.Count == pos1.Count + pos2.Count);
        return posResult;

        Dictionary<string, IReadOnlyList<Position>> GetPositionsFromBalances(
            IEnumerable<HyperLiquidBalance> balances,
            AssetType assetType = AssetType.Spot)
        {
            return balances.GroupBy(x => x.Asset)
                .ToDictionary(
                    g => g.Key,
                    IReadOnlyList<Position> (g) =>
                        g.Select(x => new Position(x.Asset, x.Asset, assetType, x.Total)).ToArray());
        }

        Dictionary<string, IReadOnlyList<Position>> GetPositionsFromPositions(
            IEnumerable<HyperLiquidPosition> positions,
            AssetType assetType = AssetType.Future)
        {
            return positions.GroupBy(x => x.Position.Symbol)
                .ToDictionary(
                    g => g.Key,
                    IReadOnlyList<Position> (g) => g.Select(x => new Position(
                            x.Position.Symbol,
                            x.Position.Symbol,
                            assetType,
                            x.Position.PositionQuantity.GetValueOrDefault()))
                        .ToArray());
        }
    }

    public async Task<IDictionary<string, IReadOnlyList<MarketInfo>>> GetMarketsAsync(
        ISet<string>? baseAssetFilter = null,
        string? quoteCurrency = null,
        AssetPermissions assetPermissions = AssetPermissions.PerpetualFutures,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting markets with baseAssetFilter: {BaseAssetFilter}, quoteCurrency: {QuoteCurrency}, assetPermissions: {AssetPermissions}",
            baseAssetFilter,
            quoteCurrency,
            assetPermissions);

        var futuresExchangeInfo = await GetDataAsync(
            () => _hyperliquidClient.FuturesApi.ExchangeData.GetExchangeInfoAndTickersAsync(ct: ct),
            "Could not get futures exchange info and tickers");

        var tickersAndPrices = await Task.WhenAll(
            futuresExchangeInfo.Tickers
                .Where(ticker => baseAssetFilter == null || baseAssetFilter.Contains(ticker.Symbol))
                .Select(async ticker => (ticker, await GetOrderBookAsync(ticker.Symbol, ct))));

        _logger.LogDebug("Retrieved {Count} tickers and prices", tickersAndPrices.Length);

        return tickersAndPrices
            .ToDictionary<(HyperLiquidFuturesTicker Ticker, HyperLiquidOrderBook OrderBook), string,
                IReadOnlyList<MarketInfo>>(
                tuple => tuple.Ticker.Symbol,
                tuple =>
                [
                    new MarketInfo(
                        tuple.Ticker.Symbol,
                        tuple.Ticker.Symbol,
                        "USD",
                        AssetType.Future,
                        DateTime.UtcNow,
                        Ask: tuple.OrderBook.Levels.Asks[0].Price,
                        Bid: tuple.OrderBook.Levels.Bids[0].Price,
                        Mid: tuple.Ticker.MidPrice)
                ]);
    }

    private async Task<HyperLiquidOrderBook> GetOrderBookAsync(string symbol, CancellationToken ct)
    {
        var orderBook = await GetDataAsync(
            () => _hyperliquidClient.FuturesApi.ExchangeData.GetOrderBookAsync(symbol, ct: ct),
            $"Could not get order book for {symbol}");

        return orderBook;
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

    private async Task<WebCallOrderResultWrapper> PlaceFuturesOrderAsync(
        Trade trade,
        OrderSide orderSide,
        decimal quantity,
        decimal? price,
        CancellationToken ct)
    {
        var result = await _hyperliquidClient.FuturesApi.Trading.PlaceOrderAsync(
            trade.AssetName,
            orderSide,
            price.HasValue ? OrderType.Limit : OrderType.Market,
            quantity,
            price.GetValueOrDefault(),
            ct: ct);

        return Wrap(result);
    }

    private async Task<WebCallOrderResultWrapper> PlaceSpotOrderAsync(
        Trade trade,
        OrderSide orderSide,
        decimal quantity,
        decimal? price,
        CancellationToken ct)
    {
        var result = await _hyperliquidClient.SpotApi.Trading.PlaceOrderAsync(
            trade.AssetName,
            orderSide,
            price.HasValue ? OrderType.Limit : OrderType.Market,
            quantity,
            price.GetValueOrDefault(),
            ct: ct);

        return Wrap(result);
    }

    private static WebCallOrderResultWrapper Wrap(WebCallResult<HyperLiquidOrderResult> result) =>
        new(
            result.Success,
            result.Error,
            result.ResponseStatusCode,
            result.Data.OrderId,
            result.Data.Status.ToYolo(),
            result.Data.AveragePrice,
            result.Data.FilledQuantity);
}