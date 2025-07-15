using CryptoExchange.Net.Objects;
using HyperLiquid.Net.Clients;
using HyperLiquid.Net.Enums;
using HyperLiquid.Net.Objects.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YoloAbstractions;
using YoloBroker.Hyperliquid.Exceptions;
using YoloBroker.Hyperliquid.Extensions;
using YoloBroker.Interface;
using OrderSide = HyperLiquid.Net.Enums.OrderSide;
using OrderStatus = YoloAbstractions.OrderStatus;

namespace YoloBroker.Hyperliquid;

public class HyperliquidBroker : IYoloBroker
{
    private readonly HyperLiquidRestClient _hyperliquidClient;
    private readonly HyperLiquidSocketClient _hyperliquidSocketClient;
    private readonly ILogger<HyperliquidBroker> _logger;
    private bool _disposed;

    public HyperliquidBroker(
        HyperLiquidRestClient hyperliquidClient,
        HyperLiquidSocketClient hyperliquidSocketClient,
        ILogger<HyperliquidBroker> logger) 
    {
        _hyperliquidClient = hyperliquidClient;
        _hyperliquidSocketClient = hyperliquidSocketClient;
        _logger = logger;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
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
                    ct),
                { AssetType: AssetType.Future } => await PlaceFuturesOrderAsync(
                    trade,
                    orderSide,
                    quantity,
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

    public async Task<Dictionary<long, Order>> GetOrdersAsync(CancellationToken ct)
    {
        var orders =
            await GetDataAsync(() => _hyperliquidClient.FuturesApi.Trading.GetOpenOrdersExtendedAsync(ct: ct),
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

    public async Task<IDictionary<string, IEnumerable<Position>>> GetPositionsAsync(
        CancellationToken ct = default)
    {
        var balances =
            await GetDataAsync(() => _hyperliquidClient.SpotApi.Account.GetBalancesAsync(ct: ct),
                "Could not get account info");

        return balances.ToDictionary<HyperLiquidBalance, string, IEnumerable<Position>>(
            x => x.Asset,
            x => [new Position(x.Asset, x.Asset, AssetType.Future, x.Total)]);
    }

    public async Task<IDictionary<string, IEnumerable<MarketInfo>>> GetMarketsAsync(
        ISet<string>? baseAssetFilter = null,
        string? quoteCurrency = null,
        AssetPermissions assetPermissions = AssetPermissions.All,
        CancellationToken ct = default)
    {
        var futuresExchangeInfo = await GetDataAsync(
            () => _hyperliquidClient.FuturesApi.ExchangeData.GetExchangeInfoAndTickersAsync(ct: ct),
            "Could not get futures exchange info and tickers");

        return futuresExchangeInfo.Tickers
            .Where(ticker => baseAssetFilter == null || baseAssetFilter.Contains(ticker.Symbol))
            .ToDictionary<HyperLiquidFuturesTicker, string, IEnumerable<MarketInfo>>(
                ticker => ticker.Symbol,
                ticker =>
                [
                    new MarketInfo(
                        ticker.Symbol,
                        ticker.Symbol,
                        "USDC",
                        AssetType.Future,
                        DateTime.UtcNow,
                        Mid: ticker.MidPrice)
                ]);
    }

    protected virtual void Dispose(bool disposing)
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
        CancellationToken ct)
    {
        var result = await _hyperliquidClient.FuturesApi.Trading.PlaceOrderAsync(
            trade.AssetName,
            orderSide,
            OrderType.Market,
            quantity,
            price: 0,
            ct: ct);

        return Wrap(result);
    }

    private async Task<WebCallOrderResultWrapper> PlaceSpotOrderAsync(
        Trade trade,
        OrderSide orderSide,
        decimal quantity,
        CancellationToken ct)
    {
        var result = await _hyperliquidClient.SpotApi.Trading.PlaceOrderAsync(
            trade.AssetName,
            orderSide,
            OrderType.Market,
            quantity,
            price: 0,
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