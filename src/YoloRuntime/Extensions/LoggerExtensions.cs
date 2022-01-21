using Microsoft.Extensions.Logging;
using YoloAbstractions;

namespace YoloRuntime;

public static partial class LoggerExtensions
{
    [LoggerMessage(
        EventId = WellKnown.TradeEventIds.OrderCancelled,
        Level = LogLevel.Information,
        Message = "Order cancelled: {Order}")]
    public static partial void OrderCancelled(
        this ILogger logger,
        Order order);
    
}