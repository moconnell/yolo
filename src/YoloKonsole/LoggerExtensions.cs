using Microsoft.Extensions.Logging;
using YoloAbstractions;

namespace YoloKonsole;

public static partial class LoggerExtensions
{
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
        Message =
            "({Token}): {Error} ({ErrorCode})")]
    public static partial void OrderError(
        this ILogger logger,
        string token,
        string? error,
        int? errorCode);
}