namespace YoloAbstractions;

public sealed record BrokerAccountContext(string? Address, string? VaultAddress, bool IsTestnet);