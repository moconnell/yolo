using System;

namespace YoloAbstractions;

public record OrderUpdate(
    string AssetName,
    OrderUpdateType Type, // Created, PartiallyFilled, Filled, Cancelled, TimedOut
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
