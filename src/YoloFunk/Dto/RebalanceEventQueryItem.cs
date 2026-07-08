namespace YoloFunk.Dto;

public sealed record RebalanceEventQueryItem(
    string RunId,
    string StrategyName,
    DateTimeOffset TimestampUtc,
    int Sequence,
    string EventType,
    string Level,
    string Summary,
    string? WalletAddress,
    string? VaultAddress,
    string? Coin,
    string? ClientOrderId,
    string? OrderId,
    string PayloadJson);
