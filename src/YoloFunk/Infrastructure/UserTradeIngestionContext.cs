using YoloAbstractions;

namespace YoloFunk.Infrastructure;

public sealed record UserTradeIngestionContext(
    string StrategyName,
    Exchange Exchange,
    string Network,
    string Address,
    string? VaultAddress);
