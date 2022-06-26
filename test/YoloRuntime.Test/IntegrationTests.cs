using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using FTX.Net.Enums;
using FTX.Net.Interfaces.Clients;
using FTX.Net.Objects.Models;
using FTX.Net.Objects.Models.Socket;
using Microsoft.Extensions.Logging;
using Moq;
using Snapshooter.Xunit;
using Xunit;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloBroker.Ftx;
using YoloRuntime.Test.Data;
using YoloRuntime.Test.Mocks;
using YoloTestUtils;
using YoloTrades;
using YoloWeights;
using OrderStatus = FTX.Net.Enums.OrderStatus;
using Weight = YoloAbstractions.Weight;

namespace YoloRuntime.Test;

public class IntegrationTests
{
    [Theory]
    [InlineData("Data/json/001", 10000, 0.5, 0.02)]
    public async Task ShouldRebalance(
        string path,
        decimal nominalCash,
        decimal spreadSplit,
        decimal tradeBuffer,
        AssetPermissions assetPermissions = AssetPermissions.All,
        bool postOnly = true,
        string quoteCurrency = "USD")
    {
        // arrange
        async Task<WebCallResult<IEnumerable<T>?>> Response<T>(string file)
        {
            var data = (await $"{path}/{file}".DeserializeAsync<T[]>())?.AsEnumerable();
            return data.ToWebCallResult();
        }

        var ftxOrderId = 1;

        var ftxClient = new Mock<IFTXClient>();
        ftxClient.Setup(x => x.TradeApi.Trading.GetOpenOrdersAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(await Response<FTXOrder>("openorders.json"));
        ftxClient.Setup(x => x.TradeApi.Account.GetPositionsAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(await Response<FTXPosition>("positions.json"));
        ftxClient.Setup(x => x.TradeApi.Account.GetBalancesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(await Response<FTXBalance>("balances.json"));
        ftxClient.Setup(x => x.TradeApi.ExchangeData.GetSymbolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(await Response<FTXSymbol>("symbols.json"));
        ftxClient.Setup(
                x => x.TradeApi.Trading.PlaceOrderAsync(
                    It.IsAny<string>(),
                    It.IsAny<FTX.Net.Enums.OrderSide>(),
                    It.IsAny<OrderType>(),
                    It.IsAny<decimal>(),
                    It.IsAny<decimal?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<string?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (
                        string symbol,
                        FTX.Net.Enums.OrderSide side,
                        OrderType type,
                        decimal quantity,
                        decimal? price,
                        bool? reduceOnly,
                        bool? immediateOrCancel,
                        bool? postOnly,
                        string? clientOrderId,
                        bool? _,
                        string? _,
                        CancellationToken _) =>
                    new FTXOrder
                    {
                        Id = ftxOrderId++,
                        Symbol = symbol,
                        Side = side,
                        Type = type,
                        Quantity = quantity,
                        QuantityRemaining = quantity,
                        Price = price,
                        ReduceOnly = reduceOnly.GetValueOrDefault(),
                        ImmediateOrCancel = immediateOrCancel.GetValueOrDefault(),
                        PostOnly = postOnly.GetValueOrDefault(),
                        ClientOrderId = clientOrderId,
                        Status = OrderStatus.New
                    }.ToWebCallResult());

        List<Action<DataEvent<FTXOrder>>> orderUpdateHandlers = new List<Action<DataEvent<FTXOrder>>>();
        var tickerUpdateHandlers = new Dictionary<string, Action<DataEvent<FTXStreamTicker>>>();

        var ftxSocketClient = new Mock<IFTXSocketClient>();
        ftxSocketClient
            .Setup(
                x => x.Streams.SubscribeToOrderUpdatesAsync(
                    It.IsAny<Action<DataEvent<FTXOrder>>>(),
                    It.IsAny<CancellationToken>()))
            .Callback<Action<DataEvent<FTXOrder>>, CancellationToken>(
                (handler, _) => { orderUpdateHandlers.Add(handler); })
            .ReturnsAsync(new CallResult<UpdateSubscription>(new MockUpdateSubscription()));

        ftxSocketClient.Setup(
                x =>
                    x.Streams.SubscribeToTickerUpdatesAsync(
                        It.IsAny<string>(),
                        It.IsAny<Action<DataEvent<FTXStreamTicker>>>(),
                        It.IsAny<CancellationToken>()))
            .Callback<string, Action<DataEvent<FTXStreamTicker>>, CancellationToken>(
                (symbol, handler, _) => { tickerUpdateHandlers[symbol] = handler; });

        var ftxBrokerLogger = new Mock<ILogger<FtxBroker>>();
        var broker = new FtxBroker(
            ftxClient.Object,
            ftxSocketClient.Object,
            ftxBrokerLogger.Object,
            assetPermissions,
            quoteCurrency,
            postOnly);

        var tradeFactoryLogger = new Mock<ILogger<TradeFactory>>();
        var yoloConfig = new YoloConfig
        {
            AssetPermissions = assetPermissions,
            BaseAsset = quoteCurrency,
            NominalCash = nominalCash,
            SpreadSplit = spreadSplit,
            TradeBuffer = tradeBuffer,
            UnfilledOrderTimeout = TimeSpan.FromSeconds(1)
        };
        var tradeFactory = new TradeFactory(tradeFactoryLogger.Object, yoloConfig);

        var weights = (await $"{path}/weights.json".DeserializeAsync<Weight[]>())
            .ToDictionary(x => x.BaseAsset);
        var weightsService = new Mock<IYoloWeightsService>();
        weightsService.Setup(x => x.GetWeightsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(weights);

        var runtimeLogger = new Mock<ILogger<Runtime>>();
        var runtime = new Runtime(weightsService.Object, broker, tradeFactory, yoloConfig, runtimeLogger.Object);
        var tradeResults = new List<TradeResult>();
        runtime.TradeUpdates.Subscribe(tradeResults.Add);

        (DateTime timestamp, Action action) ToTimeStampedAction<T>(TestDataEvent<T> e, Action<DataEvent<T>> action) =>
            (e.Timestamp, () => action(e.ToDataEvent()));

        var orderUpdates =
            (await $"{path}/streams/orders.json".DeserializeAsync<TestDataEvent<FTXOrder>[]>())
            .Select(e => ToTimeStampedAction(e, dataEvent => orderUpdateHandlers.ForEach(x => x(dataEvent))));
        var tickerUpdates = (await $"{path}/streams/tickers.json".DeserializeAsync<TestDataEvent<FTXStreamTicker>[]>())
            .Select(e => ToTimeStampedAction(e, de => tickerUpdateHandlers[de.Topic](de)));
        var updates = orderUpdates.Union(tickerUpdates).OrderBy(x => x.timestamp);

        // act
        var trades = await runtime.RebalanceAsync(CancellationToken.None);

        var tradesTask = Task.Factory
            .StartNew(async () => await runtime.PlaceTradesAsync(trades, CancellationToken.None))
            .Unwrap();

        var updatesTask = Task.Factory.StartNew(
                async () =>
                {
                    foreach (var (_, action) in updates)
                    {
                        await Task.Delay(50);
                        action();
                    }
                })
            .Unwrap();

        await Task.WhenAll(updatesTask, tradesTask);

        // assert
        tradeResults.MatchSnapshot(
            $"ShouldCalculateTrades_{path.Replace("/", "_")}",
            options => options.IgnoreFields("[*].Trade.Id")
        );
    }
}