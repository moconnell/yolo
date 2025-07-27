using System.Net;
using CryptoExchange.Net.Objects;

namespace YoloBroker.Hyperliquid;

public record WebCallResultWrapper<T>(
    bool Success,
    Error? Error,
    HttpStatusCode? ResponseStatusCode,
    T OrderResult
    );

public record OrderResult(
    long? OrderId,
    YoloAbstractions.OrderStatus OrderStatus,
    decimal? AveragePrice,
    decimal? FilledQuantity);