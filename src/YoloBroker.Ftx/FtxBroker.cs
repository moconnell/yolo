using System.Runtime.CompilerServices;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using FTX.Net;
using FTX.Net.Enums;
using FTX.Net.Objects;
using Microsoft.Extensions.Configuration;
using YoloAbstractions;
using YoloBroker.Ftx.Config;
using YoloBroker.Ftx.Exceptions;
using YoloBroker.Ftx.Extensions;
using OrderSide = FTX.Net.Enums.OrderSide;

namespace YoloBroker.Ftx;

public class FtxBroker : IYoloBroker
{
    private readonly FTXClient _ftxClient;
    private readonly FTXSocketClient _ftxSocketClient;
    private bool _disposed;

    public FtxBroker(IConfiguration configuration)
        : this(configuration.GetFtxConfig())
    {
    }

    public FtxBroker(FtxConfig config)
    {
        var credentials = new ApiCredentials(config.ApiKey, config.Secret);

        _ftxClient = new FTXClient(
            new FTXClientOptions
            {
                ApiCredentials = credentials,
                BaseAddress = config.BaseAddress,
                SubaccountName = config.SubAccount
            });

        _ftxSocketClient = new FTXSocketClient(
            new FTXSocketClientOptions
            {
                ApiCredentials = credentials,
                BaseAddress = config.BaseAddress,
                SubaccountName = config.SubAccount
            });

        PostOnly = config.PostOnly;
    }
    
    private bool? PostOnly { get; }

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
            await Task.Delay(123, ct); // throttle as FTX errors if > 2 orders in 200ms

            var orderSide = trade.Amount < 0 ? OrderSide.Sell : OrderSide.Buy;
            var orderType = trade.LimitPrice.HasValue ? OrderType.Limit : OrderType.Market;
            var quantity = Math.Abs(trade.Amount);

            var result = await _ftxClient.PlaceOrderAsync(
                trade.AssetName,
                orderSide,
                orderType,
                quantity,
                trade.LimitPrice,
                postOnly: PostOnly == true && trade.PostPrice != false,
                ct: ct);

            yield return new TradeResult(
                trade,
                result.Success,
                result.Data.ToOrder(),
                result.Error?.Message,
                result.Error?.Code);
        }
    }

    public async Task<Dictionary<long, Order>> GetOrdersAsync(CancellationToken ct)
    {
        var orders =
            await GetDataAsync(() => _ftxClient.GetOpenOrdersAsync(ct: ct),
                "Could not get open orders");

        return orders.ToDictionary(x => x.Id, x => x.ToOrder()!);
    }

    public async Task<IDictionary<string, IEnumerable<Position>>> GetPositionsAsync(
        CancellationToken ct = default)
    {
        var positions =
            await GetDataAsync(() => _ftxClient.GetPositionsAsync(ct: ct),
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
            () => _ftxClient.GetBalancesAsync(ct: ct),
            "Could not get holdings");

        foreach (var holding in holdings)
        {
            var position = new Position(
                holding.Asset,
                holding.Asset,
                AssetType.Spot,
                holding.Total);

            if (result.TryGetValue(holding.Asset, out var positionsList))
            {
                (positionsList as List<Position>)?.Add(position);
                //result[holding.Asset] = positionsList;
            }
            else
            {
                result[holding.Asset] = new List<Position>
                {
                    position
                };
            }
        }

        return result;
    }

    public async Task<IDictionary<string, IEnumerable<MarketInfo>>> GetMarketsAsync(
        ISet<string>? baseAssetFilter = null,
        string? quoteCurrency = null,
        AssetPermissions assetPermissions = AssetPermissions.All,
        CancellationToken ct = default)
    {
        var symbols = await GetDataAsync(
            () => _ftxClient.GetSymbolsAsync(ct),
            "Unable to get symbols"
        );

        bool Filter(FTXSymbol s)
        {
            if (quoteCurrency is { } &&
                s.QuoteCurrency is { } &&
                quoteCurrency != s.QuoteCurrency)
                return false;

            if (baseAssetFilter is { } &&
                !baseAssetFilter.Contains(s.BaseCurrency ?? s.Underlying))
                return false;

            var expiry = s.GetExpiry();

            return s.Type switch
            {
                SymbolType.Future when expiry.HasValue => assetPermissions.HasFlag(
                    AssetPermissions.ExpiringFutures),
                SymbolType.Future when !expiry.HasValue => assetPermissions.HasFlag(
                    AssetPermissions.PerpetualFutures),
                SymbolType.Spot => assetPermissions.HasFlag(AssetPermissions.LongSpot) ||
                                   assetPermissions.HasFlag(AssetPermissions.ShortSpot),
                _ => throw new ArgumentOutOfRangeException(nameof(s.Type),
                    s.Type,
                    "unknown asset type")
            };
        }


        return symbols
            .Where(Filter)
            .GroupBy(s => s.BaseCurrency ?? s.Underlying)
            .ToDictionary(
                g => g.Key,
                g => g.Select(s =>
                    new MarketInfo(
                        s.Name,
                        g.Key,
                        s.QuoteCurrency,
                        s.Type.ToAssetType(),
                        s.PriceStep,
                        s.QuantityStep,
                        s.MinProvideSize,
                        s.BestAsk,
                        s.BestBid,
                        s.LastPrice,
                        s.GetExpiry(),
                        DateTime.UtcNow)));
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _ftxClient.Dispose();
            _ftxSocketClient.Dispose();
        }

        _disposed = true;
    }

    private static async Task<T> GetDataAsync<T>(
        Func<Task<WebCallResult<T>>> webCallFunc,
        string exceptionMessage)
    {
        var result = await webCallFunc();

        if (!result.Success)
            throw new FtxException(exceptionMessage, result);

        return result.Data;
    }
}