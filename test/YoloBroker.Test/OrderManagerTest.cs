using System.Threading.Channels;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;
using YoloAbstractions;
using YoloAbstractions.Interfaces;
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
            MaxRepriceRetries: 2);

        var updates = await CollectUpdatesAsync(sut.ManageOrdersAsync([trade], settings, CreateAdvisor(null)));

        updates.Count.ShouldBe(1);
        updates[0].Type.ShouldBe(OrderUpdateType.Created);
        broker.Verify(x => x.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        broker.Verify(x => x.PlaceTradeAsync(It.IsAny<Trade>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ManageOrdersAsync_WhenAdvisorReturnsReplacementTrade_ShouldPlaceIt()
    {
        var trade = CreateLimitTrade(clientOrderId: "c2", amount: 10m, limitPrice: 100m);
        var partiallyFilledOrder = CreateOrder(id: 1002, clientId: "c2", status: OrderStatus.WaitingFill, amount: 10m, filled: 4m);
        var replacementTrade = trade with { Amount = 6m, OrderType = OrderType.Market, LimitPrice = null };

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
                    Order: CreateOrder(id: 2002, clientId: marketTrade.ClientOrderId, status: OrderStatus.Open, amount: marketTrade.AbsoluteAmount, filled: 0m)));

        var sut = CreateSut(broker.Object);
        var settings = new OrderManagementSettings(
            UnfilledOrderTimeout: TimeSpan.FromMilliseconds(20),
            MaxRepriceRetries: 2);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var updates = await CollectUpdatesUntilCanceledAsync(sut.ManageOrdersAsync([trade], settings, CreateAdvisor(replacementTrade), cts.Token));

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
    public async Task ManageOrdersAsync_WhenAdvisorReturnsNull_ShouldCancelAndEmitTimedOut()
    {
        var trade = CreateLimitTrade(clientOrderId: "c2b", amount: 10m, limitPrice: 100m);
        var openOrder = CreateOrder(id: 10022, clientId: "c2b", status: OrderStatus.Open, amount: 10m, filled: 0m);

        var broker = new Mock<IYoloBroker>();
        broker.Setup(x => x.SubscribeOrderUpdatesAsync(It.IsAny<CancellationToken>()))
            .Returns(EmptyEventsAsync);
        broker.Setup(x => x.PlaceTradesAsync(It.IsAny<IEnumerable<Trade>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Trade> _, CancellationToken _) =>
                ToAsyncEnumerable(new TradeResult(trade, Success: true, Order: openOrder)));
        broker.Setup(x => x.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(broker.Object);
        var settings = new OrderManagementSettings(
            UnfilledOrderTimeout: TimeSpan.FromMilliseconds(20),
            MaxRepriceRetries: 2);

        var updates = await CollectUpdatesAsync(sut.ManageOrdersAsync([trade], settings, CreateAdvisor(null)));

        updates.ShouldContain(x => x.Type == OrderUpdateType.Created);
        updates.ShouldContain(x => x.Type == OrderUpdateType.TimedOut);
        broker.Verify(x => x.CancelOrderAsync(openOrder, It.IsAny<CancellationToken>()), Times.Once);
        broker.Verify(x => x.PlaceTradeAsync(It.IsAny<Trade>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ManageOrdersAsync_WhenReplacementTradeFails_ShouldEmitErrorUpdate()
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
            MaxRepriceRetries: 2);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var updates = await CollectUpdatesUntilCanceledAsync(sut.ManageOrdersAsync([trade], settings, CreateAdvisor(trade with { Amount = 5m, OrderType = OrderType.Market, LimitPrice = null }), cts.Token));

        var errorUpdate = updates.Single(x => x.Type == OrderUpdateType.Error);
        errorUpdate.Error.ShouldNotBeNull();
        errorUpdate.Error!.Message.ShouldContain("fallback failed");
    }

    [Fact]
    public async Task ManageOrdersAsync_WhenAdvisorReturnsSellTrade_ShouldPreserveSign()
    {
        var trade = CreateLimitTrade(clientOrderId: "c3sell", amount: -10m, limitPrice: 50m);
        var openOrder = CreateOrder(id: 10031, clientId: "c3sell", status: OrderStatus.Open, amount: 10m, filled: 0m);
        var replacementTrade = trade with { Amount = -10m, OrderType = OrderType.Market, LimitPrice = null };

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
                new TradeResult(
                    marketTrade,
                    Success: true,
                    Order: CreateOrder(
                        id: 20031,
                        clientId: marketTrade.ClientOrderId,
                        status: OrderStatus.Open,
                        amount: marketTrade.AbsoluteAmount,
                        filled: 0m)));

        var sut = CreateSut(broker.Object);
        var settings = new OrderManagementSettings(
            UnfilledOrderTimeout: TimeSpan.FromMilliseconds(20),
            MaxRepriceRetries: 2);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var updates = await CollectUpdatesUntilCanceledAsync(sut.ManageOrdersAsync([trade], settings, CreateAdvisor(replacementTrade), cts.Token));

        updates.ShouldContain(x => x.Type == OrderUpdateType.MarketOrderPlaced);
        broker.Verify(x => x.PlaceTradeAsync(
                It.Is<Trade>(t =>
                    t.Symbol == trade.Symbol &&
                    t.OrderType == OrderType.Market &&
                    t.LimitPrice == null &&
                    t.Amount == -10m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ManageOrdersAsync_WhenRepriceRetriesExceeded_ShouldEscalateToMarket()
    {
        var trade = CreateLimitTrade(clientOrderId: "c3retry", amount: 8m, limitPrice: 50m);
        var firstOpenOrder = CreateOrder(id: 10041, clientId: "c3retry", status: OrderStatus.Open, amount: 8m, filled: 0m);
        var repriceTrade = trade with { LimitPrice = 49m, OrderType = OrderType.Limit };
        var replacementOpenOrder = CreateOrder(id: 10042, clientId: "c3retry", status: OrderStatus.Open, amount: 8m, filled: 0m);

        var broker = new Mock<IYoloBroker>();
        broker.Setup(x => x.SubscribeOrderUpdatesAsync(It.IsAny<CancellationToken>()))
            .Returns(EmptyEventsAsync);
        broker.Setup(x => x.PlaceTradesAsync(It.IsAny<IEnumerable<Trade>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Trade> _, CancellationToken _) =>
                ToAsyncEnumerable(new TradeResult(trade, Success: true, Order: firstOpenOrder)));
        broker.Setup(x => x.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var placedReplacements = new List<Trade>();
        broker.Setup(x => x.PlaceTradeAsync(It.IsAny<Trade>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trade replacement, CancellationToken _) =>
            {
                placedReplacements.Add(replacement);

                return replacement.OrderType == OrderType.Market
                    ? new TradeResult(
                        replacement,
                        Success: true,
                        Order: CreateOrder(id: 20041, clientId: replacement.ClientOrderId, status: OrderStatus.Filled, amount: replacement.AbsoluteAmount, filled: replacement.AbsoluteAmount))
                    : new TradeResult(
                        replacement,
                        Success: true,
                        Order: replacementOpenOrder);
            });

        var sut = CreateSut(broker.Object);
        var settings = new OrderManagementSettings(
            UnfilledOrderTimeout: TimeSpan.FromMilliseconds(20),
            MaxRepriceRetries: 1);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var updates = await CollectUpdatesUntilCanceledAsync(sut.ManageOrdersAsync([trade], settings, CreateAdvisor(repriceTrade), cts.Token));

        updates.ShouldContain(x => x.Type == OrderUpdateType.Created);
        updates.ShouldContain(x => x.Type == OrderUpdateType.MarketOrderPlaced);

        placedReplacements.Count.ShouldBe(2);
        placedReplacements[0].OrderType.ShouldBe(OrderType.Limit);
        placedReplacements[1].OrderType.ShouldBe(OrderType.Market);
        placedReplacements[1].LimitPrice.ShouldBeNull();
        placedReplacements[1].ClientOrderId.ShouldBe("c3retry");
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
        var updates = await CollectUpdatesAsync(sut.ManageOrdersAsync([trade], new OrderManagementSettings(TimeSpan.FromMilliseconds(20), 1), CreateAdvisor(null)));

        updates.Count.ShouldBe(1);
        updates[0].Type.ShouldBe(OrderUpdateType.MarketOrderPlaced);
    }

    [Fact]
    public async Task ManageOrdersAsync_ShouldEstablishOrderUpdateSubscriptionBeforePlacingTrades()
    {
        var trade = CreateLimitTrade(clientOrderId: "c5", amount: 2m, limitPrice: 100m);
        var openOrder = CreateOrder(id: 1005, clientId: "c5", status: OrderStatus.Open, amount: 2m, filled: 0m);
        var subscriptionReady = false;
        var subscriptionConsumed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var broker = new Mock<IYoloBroker>();
        broker.Setup(x => x.SubscribeOrderUpdatesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => SubscriptionEventsAsync(ct));
        broker.Setup(x => x.PlaceTradesAsync(It.IsAny<IEnumerable<Trade>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Trade> _, CancellationToken _) =>
            {
                subscriptionConsumed.Task.Wait(TimeSpan.FromSeconds(1)).ShouldBeTrue();
                subscriptionReady.ShouldBeTrue();
                return ToAsyncEnumerable(new TradeResult(trade, Success: true, Order: openOrder));
            });

        async IAsyncEnumerable<BrokerOrderEvent> SubscriptionEventsAsync([EnumeratorCancellation] CancellationToken ct)
        {
            subscriptionReady = true;
            subscriptionConsumed.TrySetResult(true);
            await Task.Yield();
            yield break;
        }

        var sut = CreateSut(broker.Object);
        var settings = new OrderManagementSettings(
            UnfilledOrderTimeout: TimeSpan.FromMilliseconds(20),
            MaxRepriceRetries: 2);

        var updates = await CollectUpdatesAsync(sut.ManageOrdersAsync([trade], settings, CreateAdvisor(null)));

        updates.ShouldContain(x => x.Type == OrderUpdateType.Created);
    }

    private static OrderManager CreateSut(IYoloBroker broker)
    {
        return new OrderManager(broker, Mock.Of<ILogger<OrderManager>>());
    }

    private static ITradeAdvisor CreateAdvisor(Trade? replacementTrade)
    {
        var mock = new Mock<ITradeAdvisor>();
        mock.Setup(x => x.GetReplacementTradeAsync(It.IsAny<Trade>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(replacementTrade);
        return mock.Object;
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
