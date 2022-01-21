using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Interfaces;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using FTX.Net.Interfaces;
using FTX.Net.Objects;
using FTX.Net.Objects.Futures;
using FTX.Net.Objects.SocketObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloBroker.Ftx;
using YoloTestUtils;
using YoloTrades;

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
        async Task<WebCallResult<IEnumerable<T>>> Response<T>(string file) =>
            new(HttpStatusCode.OK, null, await $"{path}/{file}".DeserializeAsync<T[]>(), null);

        var ftxClient = new Mock<IFTXClient>();
        ftxClient.Setup(x => x.GetOpenOrdersAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(await Response<FTXOrder>("openorders.json"));
        ftxClient.Setup(x => x.GetPositionsAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(await Response<FTXPosition>("positions.json"));
        ftxClient.Setup(x => x.GetBalancesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(await Response<FTXBalance>("balances.json"));
        ftxClient.Setup(x => x.GetSymbolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(await Response<FTXSymbol>("symbols.json"));

        Action<DataEvent<FTXOrder>> orderUpdateHandler = _ => { };
        var mockSocketClient = new Mock<ISocketClient>();
        var mockWebSocket = new Mock<IWebsocket>();
        var tickerUpdateHandlers = new Dictionary<string, Action<DataEvent<FTXStreamTicker>>>();

        var ftxSocketClient = new Mock<IFTXSocketClient>();
        ftxSocketClient
            .Setup(x => x.SubscribeToOrderUpdatesAsync(It.IsAny<Action<DataEvent<FTXOrder>>>()))
            .Callback<Action<DataEvent<FTXOrder>>>(handler => { orderUpdateHandler = handler; })
            .ReturnsAsync(new CallResult<UpdateSubscription>(null, null));
        ftxSocketClient.Setup(x =>
                x.SubscribeToTickerUpdatesAsync(It.IsAny<string>(), It.IsAny<Action<DataEvent<FTXStreamTicker>>>()))
            .Callback<string, Action<DataEvent<FTXStreamTicker>>>((symbol, handler) =>
            {
                tickerUpdateHandlers[symbol] = handler;
            });

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

        var runtimeLogger = new Mock<ILogger<Runtime>>();
        var runtime = new Runtime(broker, tradeFactory, yoloConfig, runtimeLogger.Object);
        runtime.TradeUpdates.Subscribe();
        await runtime.Rebalance(weights, CancellationToken.None);
    }
}