using System;

namespace YoloAbstractions;

public record Order(
    long Id,
    string AssetName,
    DateTime Created,
    OrderSide OrderSide,
    OrderStatus OrderStatus,
    decimal Amount,
    decimal? Filled = null,
    decimal? LimitPrice = null,
    string? ClientId = null);