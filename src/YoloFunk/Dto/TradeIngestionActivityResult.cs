using YoloFunk.Infrastructure;

namespace YoloFunk.Dto;

public sealed record TradeIngestionActivityResult(
    bool Success,
    UserTradeIngestionResult? IngestionResult,
    string? ErrorMessage);
