using System;

namespace YoloAbstractions;

public record Order(
    long Id,
    string AssetName,
    DateTime Created,
    OrderStatus OrderStatus,
    decimal Amount,
    decimal AmountRemaining,
    decimal? LimitPrice,
    string? ClientId);