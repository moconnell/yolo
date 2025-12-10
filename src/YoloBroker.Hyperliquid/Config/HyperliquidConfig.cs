namespace YoloBroker.Hyperliquid.Config;

public class HyperliquidConfig
{
    public required string Address { get; init; }
    public required string PrivateKey { get; init; }
    public required bool UseTestnet { get; init; }

    public Dictionary<string, string> Aliases { get; init; } = [];
}