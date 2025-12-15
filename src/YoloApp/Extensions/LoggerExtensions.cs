using Microsoft.Extensions.Logging;
using YoloAbstractions;
using YoloApp.Constants;

namespace YoloApp.Extensions;

public static partial class LoggerExtensions
{
    [LoggerMessage(
        EventId = WellKnown.TradeEventIds.OpenOrders,
        Level = LogLevel.Warning,
        Message = "Stopping execution - open orders!\n{Orders}")]
    public static partial void OpenOrders(
        this ILogger logger,
        IEnumerable<Order> orders);

    [LoggerMessage(
        EventId = WellKnown.TradeEventIds.CancelledOrders,
        Level = LogLevel.Warning,
        Message = "Cancelled open orders!\n{Orders}")]
    public static partial void CancelledOrders(
        this ILogger logger,
        IEnumerable<Order> orders);

    [LoggerMessage(
        EventId = WellKnown.TradeEventIds.PlacedOrder,
        Level = LogLevel.Information,
        Message =
            "({Token}): successfully placed {Order}")]
    public static partial void PlacedOrder(
        this ILogger logger,
        string token,
        Order order);

    [LoggerMessage(
        EventId = WellKnown.TradeEventIds.OrderError,
        Level = LogLevel.Error,
        Message = "({Token}): {Error} ({ErrorCode})")]
    public static partial void OrderError(
        this ILogger logger,
        string token,
        string? error,
        int? errorCode);

    [LoggerMessage(
        EventId = WellKnown.TradeEventIds.OrderUpdate,
        Level = LogLevel.Information,
        Message = "({Token}): {Type} {Message}")]
    public static partial void OrderUpdate(
        this ILogger logger,
        string token,
        OrderUpdateType type,
        string? message);
}