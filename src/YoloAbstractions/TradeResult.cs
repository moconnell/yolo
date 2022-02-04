namespace YoloAbstractions;

public record TradeResult(
    Trade Trade,
    bool? Success = null,
    Order? Order = null,
    string? Error = null,
    int? ErrorCode = null);