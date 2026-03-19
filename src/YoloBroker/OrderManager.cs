using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using YoloAbstractions;
using YoloBroker.Exceptions;
using YoloBroker.Interface;

namespace YoloBroker;

public sealed class OrderManager : IOrderManager
{
    private readonly IYoloBroker _broker;
    private readonly ILogger<OrderManager> _logger;

    public OrderManager(IYoloBroker broker, ILogger<OrderManager> logger)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async IAsyncEnumerable<OrderUpdate> ManageOrdersAsync(
        IEnumerable<Trade> trades,
        OrderManagementSettings settings,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(settings);

        var pending = new ConcurrentDictionary<string, OrderTracker>();
        var eventChannel = Channel.CreateUnbounded<ManagerEvent>();

        var subscriptionPump = Task.Run(async () =>
        {
            await foreach (var evt in _broker.SubscribeOrderUpdatesAsync(ct))
            {
                if (pending.ContainsKey(evt.ClientOrderId))
                {
                    await eventChannel.Writer.WriteAsync(new ManagerEvent.Broker(evt), ct);
                }
            }
        }, ct);

        foreach (var t in trades)
        {
            var trade = string.IsNullOrWhiteSpace(t.ClientOrderId) ? t with { ClientOrderId = Guid.NewGuid().ToString() } : t;
            pending[trade.ClientOrderId!] = OrderTracker.Create(trade);
        }

        _logger.LogInformation("Managing orders for {TradeCount} trades", pending.Count);

        await foreach (var placed in _broker.PlaceTradesAsync(pending.Values.Select(x => x.Trade), ct))
        {
            yield return ToOrderUpdate(placed);

            if (placed.Success && placed.Order is not null && placed.Trade.OrderType != OrderType.Market && !placed.Order.IsCompleted())
            {
                var key = placed.Trade.ClientOrderId!;
                pending[key].AddOrder(placed.Order);
                _ = StartTimeout(key, settings.UnfilledOrderTimeout, eventChannel, ct);
            }
            else if (placed.Trade.ClientOrderId is { } doneKey)
            {
                pending.TryRemove(doneKey, out _);
            }
        }

        while (!ct.IsCancellationRequested && !pending.IsEmpty)
        {
            var evt = await eventChannel.Reader.ReadAsync(ct);

            var update = evt switch
            {
                ManagerEvent.Broker(var b) => HandleBrokerUpdate(b),
                ManagerEvent.Timeout(var clientOrderId) => await HandleTimeoutAsync(clientOrderId),
                _ => null
            };

            if (update != null)
            {
                yield return update;
            }
        }

        eventChannel.Writer.TryComplete();
        await subscriptionPump;

        _logger.LogInformation("Finished managing orders.");

        OrderUpdate? HandleBrokerUpdate(BrokerOrderEvent evt)
        {
            if (!pending.TryGetValue(evt.ClientOrderId, out var tracker))
            {
                _logger.LogWarning("Received order update for unknown ClientOrderId {clientOrderId}", evt.ClientOrderId);
                return null;
            }

            var order = evt.Order;
            var trade = tracker.Trade;

            if (order == null)
                return null;

            tracker.AddOrder(order);

            if (tracker.IsCompleted())
            {
                pending.TryRemove(evt.ClientOrderId, out _);
                return null;
            }

            return new OrderUpdate(trade.Symbol, GetOrderUpdateType(order), order);
        }

        async Task<OrderUpdate?> HandleTimeoutAsync(string clientOrderId)
        {
            if (!pending.TryGetValue(clientOrderId, out var tracker) || tracker.CurrentOrder == null)
                return null;

            if (tracker.IsCompleted())
            {
                pending.TryRemove(clientOrderId, out _);
                return null;
            }

            if (!settings.SwitchToMarketOnTimeout)
            {
                pending.TryRemove(clientOrderId, out _);
                return new OrderUpdate(
                    tracker.Trade.Symbol,
                    OrderUpdateType.TimedOut,
                    tracker.CurrentOrder with { OrderStatus = OrderStatus.Canceled },
                    Message: "Order timed out");
            }

            try
            {
                await _broker.CancelOrderAsync(tracker.CurrentOrder, ct);

                var remainingAmount = tracker.AmountRemaining;
                if (remainingAmount <= 0)
                {
                    pending.TryRemove(clientOrderId, out _);
                    return new OrderUpdate(
                        tracker.Trade.Symbol,
                        OrderUpdateType.TimedOut,
                        tracker.CurrentOrder with { OrderStatus = OrderStatus.Canceled },
                        Message: "Order timed out");
                }

                var tradeResult = await _broker.PlaceTradeAsync(
                    tracker.Trade with { Amount = remainingAmount, LimitPrice = null, OrderType = OrderType.Market },
                    ct);

                if (tradeResult.Success && tradeResult.Order is not null)
                {
                    tracker.AddOrder(tradeResult.Order);
                }
                else
                {
                    _logger.LogError("Failed to place market order for trade {trade} after timeout: {Error} ({ErrorCode})", tracker.Trade, tradeResult.Error, tradeResult.ErrorCode);
                }

                pending.TryRemove(clientOrderId, out _);
                return ToOrderUpdate(tradeResult);
            }
            catch (Exception ex)
            {
                pending.TryRemove(clientOrderId, out _);
                _logger.LogError(ex, "Failed to cancel order {clientOrderId} after timeout: {Message}", clientOrderId, ex.Message);
                return new OrderUpdate(
                    tracker.Trade.Symbol,
                    OrderUpdateType.Error,
                    tracker.CurrentOrder,
                    Error: new BrokerException($"Failed to handle timeout for {clientOrderId}: {ex.Message}", ex));
            }
        }
    }

    private static OrderUpdate ToOrderUpdate(TradeResult result)
    {
        return new OrderUpdate(
            result.Trade.Symbol,
            GetOrderUpdateType(result),
            result.Order,
            Error: result.Success
                ? null
                : new BrokerException($"{result.Error} ({result.ErrorCode.GetValueOrDefault()})"));
    }

    private static OrderUpdateType GetOrderUpdateType(TradeResult result)
    {
        if (!result.Success || result.Order == null)
        {
            return OrderUpdateType.Error;
        }

        if (result.Trade.OrderType == OrderType.Market)
        {
            return OrderUpdateType.MarketOrderPlaced;
        }

        return GetOrderUpdateType(result.Order);
    }

    private static OrderUpdateType GetOrderUpdateType(Order order)
    {
        return order.OrderStatus switch
        {
            OrderStatus.Canceled or OrderStatus.MarginCanceled => OrderUpdateType.Cancelled,
            OrderStatus.Filled => OrderUpdateType.Filled,
            OrderStatus.Rejected => OrderUpdateType.Error,
            _ when order.Filled > 0 && order.Filled < order.Amount => OrderUpdateType.PartiallyFilled,
            _ => OrderUpdateType.Created
        };
    }

    private static Task StartTimeout(
        string clientOrderId,
        TimeSpan timeout,
        Channel<ManagerEvent> eventChannel,
        CancellationToken ct)
    {
        return Task.Run(
            async () =>
            {
                await Task.Delay(timeout, ct);
                await eventChannel.Writer.WriteAsync(new ManagerEvent.Timeout(clientOrderId), ct);
            },
            ct);
    }

    private class OrderTracker
    {
        private readonly Trade _trade;
        private readonly DateTime _createdAt;
        private readonly ConcurrentStack<Order> _orderHistory = new();

        private OrderTracker(Trade trade, DateTime createdAt)
        {
            _trade = trade;
            _createdAt = createdAt;
        }

        internal static OrderTracker Create(Trade trade) => new(trade, DateTime.UtcNow);

        internal Order? CurrentOrder => _orderHistory.TryPeek(out var order) ? order : null;

        internal Trade Trade => _trade;

        internal decimal AmountRemaining => _trade.Amount - _orderHistory.Sum(x => x.Filled.GetValueOrDefault());

        internal void AddOrder(Order order)
        {
            if (order != CurrentOrder)
            {
                _orderHistory.Push(order);
            }
        }

        internal bool IsCompleted()
        {
            return AmountRemaining <= 0;
        }
    }

    private abstract record ManagerEvent
    {
        public sealed record Broker(BrokerOrderEvent Event) : ManagerEvent;
        public sealed record Timeout(string ClientOrderId) : ManagerEvent;
    }
}
