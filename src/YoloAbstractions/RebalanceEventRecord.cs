namespace YoloAbstractions;

public sealed record RebalanceEventRecord
{
    public string RunId { get; init; } = string.Empty;
    public string StrategyName { get; init; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public int Sequence { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Level { get; init; } = "Info";
    public string Summary { get; init; } = string.Empty;
    public string? WalletAddress { get; init; }
    public string? VaultAddress { get; init; }
    public bool? IsTestnet { get; init; }
    public string? Coin { get; init; }
    public string? ClientOrderId { get; init; }
    public string? OrderId { get; init; }
    public string PayloadJson { get; init; } = "{}";
}
