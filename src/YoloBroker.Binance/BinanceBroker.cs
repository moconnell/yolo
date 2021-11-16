using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Objects;
using Binance.Net.Objects.Futures.FuturesData;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.SpotData;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using YoloAbstractions;

namespace YoloBroker.Binance
{
    public class BinanceBroker : IYoloBroker, IDisposable
    {
        private readonly BinanceClient _binanceClient;
        private readonly BinanceSocketClient _binanceSocketClient;
        private bool _disposed;

        public BinanceBroker(BinanceConfig config)
        {
            var credentials = new ApiCredentials(config.ApiKey, config.Secret);

            _binanceClient = new BinanceClient(
                new BinanceClientOptions
                {
                    ApiCredentials = credentials,
                    BaseAddress = config.BaseAddress,
                    BaseAddressCoinFutures = config.BaseAddressCoinFutures,
                    BaseAddressUsdtFutures = config.BaseAddressUsdtFutures
                });

            _binanceSocketClient = new BinanceSocketClient(
                new BinanceSocketClientOptions
                {
                    ApiCredentials = credentials,
                    BaseAddress = config.BaseAddressSocketClient
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

                    var result = trade switch
                    {
                        {AssetType: AssetType.Spot} => await PlaceSpotOrderAsync(
                            trade,
                            orderSide,
                            quantity,
                            ct),
                        {AssetType: AssetType.Future} => await PlaceFuturesOrderAsync(
                            trade,
                            orderSide,
                            quantity,
                            ct),
                        _ => throw new ArgumentOutOfRangeException(
                            nameof(trade.AssetType),
                            trade.AssetType,
                            "AssetType not supported")
                    };

                    return (trade, result);
                }));

            var failedResults = tradeResults
                .Where(tradeResult => !tradeResult.result.Success)
                .ToArray();

            if (failedResults.Any())
            {
                throw new BinanceException("Errors placing trades", failedResults);
            }
        }

        public async Task ConnectAsync(CancellationToken ct = default)
        {
            var listenKeyResult = await _binanceClient.Spot.UserStream.StartUserStreamAsync(ct);

            if (!listenKeyResult.Success)
            {
                throw new BinanceException("Could not start user stream", listenKeyResult);
            }

            var subscribeResult =
                await _binanceSocketClient.Spot.SubscribeToUserDataUpdatesAsync(
                    listenKeyResult.Data,
                    Console.WriteLine,
                    Console.WriteLine,
                    Console.WriteLine,
                    Console.WriteLine);

            if (!subscribeResult.Success)
            {
                throw new BinanceException("Could not subscribe to user updates", subscribeResult);
            }
        }

        public async Task<Dictionary<string, IEnumerable<Price>>> GetPricesAsync(
            CancellationToken ct = default)
        {
            async Task<IDictionary<string, Price>> GetPricesImplAsync(
                AssetType assetType,
                Func<CancellationToken, Task<WebCallResult<IEnumerable<BinancePrice>>>>
                    fetchPricesFunc)
            {
                var result = await fetchPricesFunc(ct);

                if (!result.Success)
                {
                    throw new BinanceException("Could not fetch prices", result);
                }

                return result.Data.ToDictionary(
                    p => p.Symbol,
                    p => new Price(p.Symbol, assetType, p.Price, p.Timestamp));
            }

            var spotPrices = await GetPricesImplAsync(
                AssetType.Spot,
                _binanceClient.Spot.Market.GetPricesAsync);

            var futuresUsdtPrices = await GetPricesImplAsync(
                AssetType.Future,
                _binanceClient.FuturesUsdt.Market.GetPricesAsync);

            return spotPrices
                .Union(futuresUsdtPrices)
                .GroupBy(kvp => kvp.Key)
                .ToDictionary(g => g.Key, g => g.Select(kvp => kvp.Value));
        }

        public async Task<IDictionary<string, Position>> GetPositionsAsync(
            CancellationToken ct = default)
        {
            var accountData =
                await GetDataAsync(() => _binanceClient.General.GetAccountInfoAsync(ct: ct),
                    "Could not get account info");

            return accountData.Balances.ToDictionary(
                x => x.Asset,
                x => new Position(x.Asset, AssetType.Spot, x.Total));
        }

        public async Task<IDictionary<string, SymbolInfo>> GetSymbolsAsync(
            IEnumerable<string>? symbols = null,
            CancellationToken ct = default)
        {
            var spotExchangeInfo = await GetDataAsync<BinanceExchangeInfo>(
                symbols is null
                    ? () => _binanceClient.Spot.System.GetExchangeInfoAsync(ct)
                    : () => _binanceClient.Spot.System.GetExchangeInfoAsync(symbols, ct),
                "Could not get spot exchange info");

            var spotSymbols = spotExchangeInfo.Symbols.Select(
                x => new SymbolInfo(
                    x.BaseAsset,
                    x.QuoteAsset,
                    AssetType.Spot,
                    (x.LotSizeFilter?.StepSize ?? x.MarketLotSizeFilter?.StepSize) ??
                    1));

            var futuresExchangeInfo = await GetDataAsync(
                () => _binanceClient.FuturesUsdt.System.GetExchangeInfoAsync(ct),
                "Could not get futures exchange info");

            var futuresSymbols = futuresExchangeInfo.Symbols.Select(
                x => new SymbolInfo(
                    x.BaseAsset,
                    x.QuoteAsset,
                    AssetType.Future,
                    x.LotSizeFilter.StepSize));

            return spotSymbols.Union(futuresSymbols)
                .ToDictionary(x => x.Key, x => x);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _binanceClient.Dispose();
                _binanceSocketClient.Dispose();
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
                throw new BinanceException(exceptionMessage, result);
            }

            return result.Data;
        }

        private async Task<WebCallResultWrapper> PlaceFuturesOrderAsync(
            Trade trade,
            OrderSide orderSide,
            decimal quantity,
            CancellationToken ct)
        {
            var result = await _binanceClient.FuturesUsdt.Order.PlaceOrderAsync(
                trade.AssetName,
                orderSide,
                OrderType.Market,
                quantity,
                ct: ct);

            return Wrap(result);
        }

        private async Task<WebCallResultWrapper> PlaceSpotOrderAsync(
            Trade trade,
            OrderSide orderSide,
            decimal quantity,
            CancellationToken ct)
        {
            var result = await _binanceClient.Spot.Order.PlaceOrderAsync(
                trade.AssetName,
                orderSide,
                OrderType.Market,
                quantity,
                ct: ct);

            return Wrap(result);
        }

        private static WebCallResultWrapper Wrap(WebCallResult<BinancePlacedOrder> result)
        {
            return new WebCallResultWrapper(
                result.Success,
                result.Error,
                result.ResponseStatusCode,
                result.Data?.ClientOrderId,
                result.Data?.OrderId);
        }

        private static WebCallResultWrapper Wrap(WebCallResult<BinanceFuturesPlacedOrder> result)
        {
            return new WebCallResultWrapper(
                result.Success,
                result.Error,
                result.ResponseStatusCode,
                result.Data?.ClientOrderId,
                result.Data?.OrderId);
        }
    }
}