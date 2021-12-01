namespace YoloAbstractions
{
    public record TradeResult(
        Trade Trade,
        bool Success,
        Order? Order = null,
        string? Error = null,
        int? ErrorCode = null);
}