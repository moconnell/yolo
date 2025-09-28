namespace Unravel.Weights.Config;

public record FactorConfig
{
    public string Id { get; init; } = "";
    public FactorType Type { get; init; }
}