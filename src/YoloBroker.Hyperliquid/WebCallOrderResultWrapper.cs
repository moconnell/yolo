using System.Net;
using CryptoExchange.Net.Objects;

namespace YoloBroker.Hyperliquid;

public record WebCallOrderResultWrapper(
    bool Success,
    Error? Error,
    HttpStatusCode? ResponseStatusCode,
    long OrderId,
    YoloAbstractions.OrderStatus OrderStatus,
    decimal? AveragePrice,
    decimal? FilledQuantity
    );