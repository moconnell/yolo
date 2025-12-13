namespace YoloBroker.Hyperliquid.Config;

public class HyperliquidConfig
{
    public required string Address { get; init; }
    public string PrivateKey { get; init; } = string.Empty;
    public required bool UseTestnet { get; init; }

    public Dictionary<string, string> Aliases { get; init; } = [];
}