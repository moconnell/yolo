namespace YoloAbstractions;

public record BrokerOrderEvent(
    string ClientOrderId,
    Order Order,
    bool Success,
    string? Error = null,
    int? ErrorCode = null);