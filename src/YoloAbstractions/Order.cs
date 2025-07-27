using System;

namespace YoloAbstractions;

public record Order(
    long Id,
    string AssetName,
    AssetType AssetType,
    DateTime Created,
    OrderSide OrderSide,
    OrderStatus OrderStatus,
    decimal Amount,
    decimal? Filled = null,
    decimal? LimitPrice = null,
    string? ClientId = null)
{
    public OrderType OrderType => LimitPrice.HasValue ? OrderType.Limit : OrderType.Market;
}