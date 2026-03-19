using System.Threading.Channels;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;
using YoloAbstractions;
using YoloBroker.Interface;

namespace YoloBroker.Test;

public class OrderManagerTest
{
    [Fact]
    public async Task ManageOrdersAsync_WhenLimitOrderFillsImmediately_ShouldNotTriggerMarketFallback()
    {
        var updatesChannel = Channel.CreateUnbounded<BrokerOrderEvent>();
        var trade = CreateLimitTrade(clientOrderId: "c1", amount: 10m, limitPrice: 100m);
        var placedOrder = CreateOrder(id: 1001, clientId: "c1", status: OrderStatus.Open, amount: 10m, filled: 0m);
        var fillOrder = CreateOrder(id: 1001, clientId: "c1", status: OrderStatus.Filled, amount: 10m, filled: 10m);

        var broker = new Mock<IYoloBroker>();
        broker.Setup(x => x.SubscribeOrderUpdatesAsync(It.IsAny<CancellationToken>()))
            .Returns(updatesChannel.Reader.ReadAllAsync);
        broker.Setup(x => x.PlaceTradesAsync(It.IsAny<IEnumerable<Trade>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Trade> _, CancellationToken _) => PlaceTradesWithEarlyFillAsync(trade, placedOrder, updatesChannel, fillOrder));

        var sut = CreateSut(broker.Object);
        var settings = new OrderManagementSettings(
            UnfilledOrderTimeout: TimeSpan.FromMilliseconds(20),
            SwitchToMarketOnTimeout: true,
            StatusCheckInterval: TimeSpan.FromMilliseconds(10));

        var updates = await CollectUpdatesAsync(sut.ManageOrdersAsync([trade], settings));

        updates.Count.ShouldBe(1);
        updates[0].Type.ShouldBe(OrderUpdateType.Created);
        broker.Verify(x => x.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        broker.Verify(x => x.PlaceTradeAsync(It.IsAny<Trade>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ManageOrdersAsync_WhenTimedOutPartiallyFilledOrder_ShouldPlaceMarketForRemainingAmount()
    {
        var trade = CreateLimitTrade(clientOrderId: "c2", amount: 10m, limitPrice: 100m);
        var partiallyFilledOrder = CreateOrder(id: 1002, clientId: "c2", status: OrderStatus.WaitingFill, amount: 10m, filled: 4m);

        var broker = new Mock<IYoloBroker>();
        broker.Setup(x => x.SubscribeOrderUpdatesAsync(It.IsAny<CancellationToken>()))
            .Returns(EmptyEventsAsync);
        broker.Setup(x => x.PlaceTradesAsync(It.IsAny<IEnumerable<Trade>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Trade> _, CancellationToken _) =>
                ToAsyncEnumerable(new TradeResult(trade, Success: true, Order: partiallyFilledOrder)));
        broker.Setup(x => x.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        broker.Setup(x => x.PlaceTradeAsync(It.IsAny<Trade>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trade marketTrade, CancellationToken _) =>
                new TradeResult(
                    marketTrade,
                    Success: true,
                    Order: CreateOrder(id: 2002, clientId: marketTrade.ClientOrderId, status: OrderStatus.Open, amount: marketTrade.Amount, filled: 0m)));

        var sut = CreateSut(broker.Object);
        var settings = new OrderManagementSettings(
            UnfilledOrderTimeout: TimeSpan.FromMilliseconds(20),
            SwitchToMarketOnTimeout: true,
            StatusCheckInterval: TimeSpan.FromMilliseconds(10));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var updates = await CollectUpdatesUntilCanceledAsync(sut.ManageOrdersAsync([trade], settings, cts.Token));

        updates.ShouldContain(x => x.Type == OrderUpdateType.MarketOrderPlaced);
        broker.Verify(x => x.CancelOrderAsync(partiallyFilledOrder, It.IsAny<CancellationToken>()), Times.Once);
        broker.Verify(x => x.PlaceTradeAsync(
                It.Is<Trade>(t =>
                    t.Symbol == trade.Symbol &&
                    t.OrderType == OrderType.Market &&
                    t.LimitPrice == null &&
                    t.Amount == 6m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ManageOrdersAsync_WhenTimedOutAndMarketFallbackDisabled_ShouldEmitTimedOutAndComplete()
    {
        var trade = CreateLimitTrade(clientOrderId: "c2b", amount: 10m, limitPrice: 100m);
        var openOrder = CreateOrder(id: 10021, clientId: "c2b", status: OrderStatus.Open, amount: 10m, filled: 0m);

        var broker = new Mock<IYoloBroker>();
        broker.Setup(x => x.SubscribeOrderUpdatesAsync(It.IsAny<CancellationToken>()))
            .Returns(EmptyEventsAsync);
        broker.Setup(x => x.PlaceTradesAsync(It.IsAny<IEnumerable<Trade>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Trade> _, CancellationToken _) =>
                ToAsyncEnumerable(new TradeResult(trade, Success: true, Order: openOrder)));

        var sut = CreateSut(broker.Object);
        var settings = new OrderManagementSettings(
            UnfilledOrderTimeout: TimeSpan.FromMilliseconds(20),
            SwitchToMarketOnTimeout: false,
            StatusCheckInterval: TimeSpan.FromMilliseconds(10));

        var updates = await CollectUpdatesAsync(sut.ManageOrdersAsync([trade], settings));

        updates.ShouldContain(x => x.Type == OrderUpdateType.Created);
        updates.ShouldContain(x => x.Type == OrderUpdateType.TimedOut);
        broker.Verify(x => x.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        broker.Verify(x => x.PlaceTradeAsync(It.IsAny<Trade>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ManageOrdersAsync_WhenMarketFallbackFails_ShouldEmitErrorUpdate()
    {
        var trade = CreateLimitTrade(clientOrderId: "c3", amount: 5m, limitPrice: 50m);
        var openOrder = CreateOrder(id: 1003, clientId: "c3", status: OrderStatus.Open, amount: 5m, filled: 0m);

        var broker = new Mock<IYoloBroker>();
        broker.Setup(x => x.SubscribeOrderUpdatesAsync(It.IsAny<CancellationToken>()))
            .Returns(EmptyEventsAsync);
        broker.Setup(x => x.PlaceTradesAsync(It.IsAny<IEnumerable<Trade>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Trade> _, CancellationToken _) =>
                ToAsyncEnumerable(new TradeResult(trade, Success: true, Order: openOrder)));
        broker.Setup(x => x.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        broker.Setup(x => x.PlaceTradeAsync(It.IsAny<Trade>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trade marketTrade, CancellationToken _) =>
                new TradeResult(marketTrade, Success: false, Error: "fallback failed", ErrorCode: 500));

        var sut = CreateSut(broker.Object);
        var settings = new OrderManagementSettings(
            UnfilledOrderTimeout: TimeSpan.FromMilliseconds(20),
            SwitchToMarketOnTimeout: true,
            StatusCheckInterval: TimeSpan.FromMilliseconds(10));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var updates = await CollectUpdatesUntilCanceledAsync(sut.ManageOrdersAsync([trade], settings, cts.Token));

        var errorUpdate = updates.Single(x => x.Type == OrderUpdateType.Error);
        errorUpdate.Error.ShouldNotBeNull();
        errorUpdate.Error!.Message.ShouldContain("fallback failed");
    }

    [Fact]
    public async Task ManageOrdersAsync_WhenInitialTradeIsMarket_ShouldEmitMarketOrderPlacedAndComplete()
    {
        var trade = new Trade(
            Symbol: "BTC",
            AssetType: AssetType.Future,
            Amount: 2m,
            LimitPrice: null,
            OrderType: OrderType.Market,
            ClientOrderId: "c4");

        var broker = new Mock<IYoloBroker>();
        broker.Setup(x => x.SubscribeOrderUpdatesAsync(It.IsAny<CancellationToken>()))
            .Returns(EmptyEventsAsync);
        broker.Setup(x => x.PlaceTradesAsync(It.IsAny<IEnumerable<Trade>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Trade> _, CancellationToken _) =>
                ToAsyncEnumerable(new TradeResult(
                    trade,
                    Success: true,
                    Order: CreateOrder(id: 1004, clientId: "c4", status: OrderStatus.Filled, amount: 2m, filled: 2m))));

        var sut = CreateSut(broker.Object);
        var updates = await CollectUpdatesAsync(sut.ManageOrdersAsync([trade], OrderManagementSettings.Default));

        updates.Count.ShouldBe(1);
        updates[0].Type.ShouldBe(OrderUpdateType.MarketOrderPlaced);
    }

    private static OrderManager CreateSut(IYoloBroker broker)
    {
        return new OrderManager(broker, Mock.Of<ILogger<OrderManager>>());
    }

    private static Trade CreateLimitTrade(string clientOrderId, decimal amount, decimal limitPrice)
    {
        return new Trade(
            Symbol: "SOL",
            AssetType: AssetType.Future,
            Amount: amount,
            LimitPrice: limitPrice,
            OrderType: OrderType.Limit,
            ClientOrderId: clientOrderId);
    }

    private static Order CreateOrder(long id, string? clientId, OrderStatus status, decimal amount, decimal filled)
    {
        return new Order(
            Id: id,
            Symbol: "SOL",
            AssetType: AssetType.Future,
            Created: DateTime.UtcNow,
            OrderSide: OrderSide.Buy,
            OrderStatus: status,
            Amount: amount,
            Filled: filled,
            LimitPrice: 100m,
            ClientId: clientId);
    }

    private static async IAsyncEnumerable<TradeResult> ToAsyncEnumerable(params TradeResult[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<BrokerOrderEvent> EmptyEventsAsync([EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<TradeResult> PlaceTradesWithEarlyFillAsync(
        Trade trade,
        Order placedOrder,
        Channel<BrokerOrderEvent> updatesChannel,
        Order fillOrder)
    {
        await updatesChannel.Writer.WriteAsync(
            new BrokerOrderEvent(trade.ClientOrderId!, fillOrder, Success: true));

        yield return new TradeResult(trade, Success: true, Order: placedOrder);
        updatesChannel.Writer.TryComplete();
    }

    private static async Task<List<OrderUpdate>> CollectUpdatesAsync(IAsyncEnumerable<OrderUpdate> stream)
    {
        var updates = new List<OrderUpdate>();
        await foreach (var update in stream)
        {
            updates.Add(update);
        }

        return updates;
    }

    private static async Task<List<OrderUpdate>> CollectUpdatesUntilCanceledAsync(IAsyncEnumerable<OrderUpdate> stream)
    {
        var updates = new List<OrderUpdate>();

        try
        {
            await foreach (var update in stream)
            {
                updates.Add(update);
            }
        }
        catch (OperationCanceledException)
        {
        }

        return updates;
    }
}
