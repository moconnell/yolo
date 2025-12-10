namespace YoloAbstractions;

public record OrderUpdate(
    string Symbol,
    OrderUpdateType Type,
    Order? Order = null,
    string? Message = null,
    Exception? Error = null);

public enum OrderUpdateType
{
    Created,
    PartiallyFilled,
    Filled,
    Cancelled,
    TimedOut,
    Error,
    MarketOrderPlaced
}
