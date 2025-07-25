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

            var result = trade switch
            {
                { AssetType: AssetType.Spot } => await PlaceSpotOrderAsync(trade, ct),
                { AssetType: AssetType.Future } => await PlaceFuturesOrderAsync(trade, ct),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(trade.AssetType),
                    trade.AssetType,
                    "AssetType not supported")
            };

            yield return new TradeResult(
                trade,
                result.Success,
                result.OrderId.HasValue ?
                    new Order(
                        result.OrderId.Value,
                        trade.AssetName,
                        DateTime.UtcNow,
                        trade.OrderSide,
                        result.OrderStatus,
                        trade.AbsoluteAmount,
                        result.FilledQuantity,
                        trade.LimitPrice,
                        trade.ClientOrderId) :
                    null,
                result.Error?.Message,
                result.Error?.Code);
        }
    }

    public async Task CancelOrderAsync(string symbol, long orderId)
    {
        ArgumentException.ThrowIfNullOrEmpty(symbol);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(orderId, 0);

        _logger.LogDebug("Cancelling order {OrderId} for symbol {Symbol}", orderId, symbol);

        await CallAsync(
            () => _hyperliquidClient.FuturesApi.Trading.CancelOrderAsync(symbol, orderId),
            "Could not cancel order");
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
                        [.. g.Select(x => new Position(x.Asset, x.Asset, assetType, x.Total))]);
        }

        Dictionary<string, IReadOnlyList<Position>> GetPositionsFromPositions(
            IEnumerable<HyperLiquidPosition> positions,
            AssetType assetType = AssetType.Future)
        {
            return positions.GroupBy(x => x.Position.Symbol)
                .ToDictionary(
                    g => g.Key,
                    IReadOnlyList<Position> (g) => [.. g.Select(x => new Position(
                            x.Position.Symbol,
                            x.Position.Symbol,
                            assetType,
                            x.Position.PositionQuantity.GetValueOrDefault()))]);
        }
    }

    public async Task<IDictionary<string, IReadOnlyList<MarketInfo>>> GetMarketsAsync(
        ISet<string>? baseAssetFilter = null,
        string quoteCurrency = "USD",
        AssetPermissions assetPermissions = AssetPermissions.SpotAndPerp,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting markets with baseAssetFilter: {BaseAssetFilter}, quoteCurrency: {QuoteCurrency}, assetPermissions: {AssetPermissions}",
            baseAssetFilter,
            quoteCurrency,
            assetPermissions);

        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>();

        if (assetPermissions.HasFlag(AssetPermissions.PerpetualFutures) || assetPermissions.HasFlag(AssetPermissions.LongSpotAndPerp) ||
            assetPermissions.HasFlag(AssetPermissions.SpotAndPerp))
        {
            markets = await GetFuturesMarketsAsync(baseAssetFilter, quoteCurrency, ct);
        }

        if (assetPermissions.HasFlag(AssetPermissions.Spot) || assetPermissions.HasFlag(AssetPermissions.LongSpot) ||
            assetPermissions.HasFlag(AssetPermissions.ShortSpot) || assetPermissions.HasFlag(AssetPermissions.SpotAndPerp))
        {
            markets = markets
                .Concat(await GetSpotMarketsAsync(baseAssetFilter, quoteCurrency, ct))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        return markets;
    }

    private async Task<Dictionary<string, IReadOnlyList<MarketInfo>>> GetSpotMarketsAsync(
        ISet<string>? baseAssetFilter,
        string? quoteCurrency,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting spot markets with baseAssetFilter: {BaseAssetFilter}, quoteCurrency: {QuoteCurrency}",
            baseAssetFilter, quoteCurrency);

        var spotExchangeInfo = await GetDataAsync(
            () => _hyperliquidClient.SpotApi.ExchangeData.GetExchangeInfoAsync(ct: ct),
            "Could not get spot exchange info");

        var tickersAndPrices = await Task.WhenAll(
            spotExchangeInfo.Symbols
            .Where(symbol => baseAssetFilter == null || baseAssetFilter.Contains(symbol.BaseAsset.Name) && symbol.QuoteAsset.Name == quoteCurrency)
            .Select(async symbol => (symbol, await GetSpotOrderBookAsync(symbol.Name, ct))));

        return tickersAndPrices
                    .ToDictionary<(HyperLiquidSymbol Symbol, HyperLiquidOrderBook OrderBook), string, IReadOnlyList<MarketInfo>>(
                        tuple => tuple.Symbol.Name,
                        tuple => [ToMarketInfo(tuple.Symbol, tuple.OrderBook)]);
    }

    private static MarketInfo ToMarketInfo(HyperLiquidSymbol symbol, HyperLiquidOrderBook orderBook)
    {
        var priceStep = Convert.ToDecimal(Math.Pow(10, -6 + symbol.QuoteAsset.PriceDecimals));
        var quantityStep = Convert.ToDecimal(Math.Pow(10, -symbol.QuoteAsset.QuantityDecimals));
        var minProvideSize = Math.Ceiling(10 / orderBook.Levels.Asks[0].Price / quantityStep) * quantityStep;

        return new MarketInfo(
                            symbol.Name,
                            symbol.BaseAsset.Name,
                            symbol.QuoteAsset.Name,
                            AssetType.Spot,
                            DateTime.UtcNow,
                            priceStep,
                            quantityStep,
                            minProvideSize,
                            Ask: orderBook.Levels.Asks[0].Price,
                            Bid: orderBook.Levels.Bids[0].Price,
                            Mid: (orderBook.Levels.Asks[0].Price + orderBook.Levels.Bids[0].Price) / 2
                        );
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
        string quoteCurrency = "USD",
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
            .ToDictionary<(HyperLiquidFuturesSymbol Symbol, HyperLiquidOrderBook OrderBook), string,
                IReadOnlyList<MarketInfo>>(
                tuple => tuple.Symbol.Name,
                tuple => [ToMarketInfo(tuple.Symbol, quoteCurrency, tuple.OrderBook)]);
    }

    private static MarketInfo ToMarketInfo(HyperLiquidFuturesSymbol symbol, string quoteCurrency, HyperLiquidOrderBook orderBook)
    {
        var priceStep = Convert.ToDecimal(Math.Pow(10, -6 + symbol.QuantityDecimals));
        var quantityStep = Convert.ToDecimal(Math.Pow(10, -symbol.QuantityDecimals));
        var minProvideSize = Math.Ceiling(10 / orderBook.Levels.Asks[0].Price / quantityStep) * quantityStep;

        return new MarketInfo(
                        symbol.Name,
                        symbol.Name,
                        quoteCurrency,
                        AssetType.Future,
                        DateTime.UtcNow,
                        priceStep,
                        quantityStep,
                        minProvideSize,
                        Ask: orderBook.Levels.Asks[0].Price,
                        Bid: orderBook.Levels.Bids[0].Price,
                        Mid: (orderBook.Levels.Asks[0].Price + orderBook.Levels.Bids[0].Price) / 2);
    }

    private async Task<HyperLiquidOrderBook> GetFuturesOrderBookAsync(string symbol, CancellationToken ct)
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

    private static async Task CallAsync(Func<Task<WebCallResult>> webCallFunc, string exceptionMessage)
    {
        var result = await webCallFunc();

        if (!result.Success)
        {
            throw new HyperliquidException(exceptionMessage, result);
        }
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

    private async Task<WebCallOrderResultWrapper> PlaceFuturesOrderAsync(Trade trade, CancellationToken ct)
    {
        OrderSide orderSide = trade.Amount < 0 ? OrderSide.Sell : OrderSide.Buy;
        OrderType orderType = trade.LimitPrice.HasValue ? OrderType.Limit : OrderType.Market;
        decimal quantity = Math.Abs(trade.Amount);
        TimeInForce timeInForce = orderType == OrderType.Market
            ? TimeInForce.ImmediateOrCancel  // For market orders
            : TimeInForce.GoodTillCanceled;  // For limit orders

        var result = await _hyperliquidClient.FuturesApi.Trading.PlaceOrderAsync(
            trade.AssetName,
            orderSide,
            orderType,
            quantity,
            trade.LimitPrice.GetValueOrDefault(),
            timeInForce,
            clientOrderId: trade.ClientOrderId,
            ct: ct);

        return Wrap(result);
    }

    private async Task<WebCallOrderResultWrapper> PlaceSpotOrderAsync(Trade trade, CancellationToken ct)
    {
        OrderSide orderSide = trade.Amount < 0 ? OrderSide.Sell : OrderSide.Buy;
        OrderType orderType = trade.LimitPrice.HasValue ? OrderType.Limit : OrderType.Market;
        decimal quantity = Math.Abs(trade.Amount);
        TimeInForce timeInForce = orderType == OrderType.Market
            ? TimeInForce.ImmediateOrCancel  // For market orders
            : TimeInForce.GoodTillCanceled;  // For limit orders

        var result = await _hyperliquidClient.SpotApi.Trading.PlaceOrderAsync(
            trade.AssetName,
            orderSide,
            orderType,
            quantity,
            trade.LimitPrice.GetValueOrDefault(),
            timeInForce,
            clientOrderId: trade.ClientOrderId,
            ct: ct);

        return Wrap(result);
    }

    private static WebCallOrderResultWrapper Wrap(WebCallResult<HyperLiquidOrderResult> result) =>
        new(
            result.Success,
            result.Error,
            result.ResponseStatusCode,
            result.Data?.OrderId,
            result.Data?.Status.ToYolo() ?? OrderStatus.Rejected,
            result.Data?.AveragePrice,
            result.Data?.FilledQuantity);
}
