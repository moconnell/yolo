using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

namespace YoloBroker.Ftx
{
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
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async Task PlaceTradesAsync(
            IEnumerable<Trade> trades,
            CancellationToken ct = default)
        {
            var tradeResults = await Task.WhenAll(
                trades.Select(async trade =>
                {
                    var orderSide = trade.Amount < 0 ? OrderSide.Sell : OrderSide.Buy;
                    var quantity = Math.Abs(trade.Amount);
                    var orderType = trade.LimitPrice.HasValue ? OrderType.Limit : OrderType.Market;

                    var result = await _ftxClient.PlaceOrderAsync(
                        trade.AssetName,
                        orderSide,
                        orderType,
                        quantity,
                        trade.LimitPrice,
                        ct: ct);

                    return (trade, result);
                }));

            var failedResults = tradeResults
                .Where(tradeResult => !tradeResult.result.Success)
                .ToArray();

            if (failedResults.Any())
            {
                throw new FtxException("Errors placing trades", failedResults);
            }
        }

        public Task ConnectAsync(CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public async Task<IDictionary<string, Position>> GetPositionsAsync(
            CancellationToken ct = default)
        {
            var positions =
                await GetDataAsync(() => _ftxClient.GetPositionsAsync(ct: ct),
                    "Could not get account info");

            var result = positions.ToDictionary(
                x => x.Future,
                x => new Position(
                    x.Future,
                    x.Future.Split("-")
                        .First(),
                    AssetType.Future,
                    x.Quantity));

            var holdings = await GetDataAsync(
                () => _ftxClient.GetBalancesAsync(ct: ct),
                "Could not get holdings");

            foreach (var holding in holdings)
            {
                result[holding.Asset] = new Position(
                    holding.Asset,
                    holding.Asset,
                    AssetType.Spot,
                    holding.Total);
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
                {
                    return false;
                }

                if (baseAssetFilter is { } &&
                    !baseAssetFilter.Contains(s.BaseCurrency ?? s.Underlying))
                {
                    return false;
                }

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
                            s.BestAsk,
                            s.BestBid,
                            s.LastPrice,
                            s.GetExpiry(),
                            DateTime.UtcNow)));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

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
            {
                throw new FtxException(exceptionMessage, result);
            }

            return result.Data;
        }
    }
}