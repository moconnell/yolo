namespace Unravel.Weights.Config;

public record UnravelConfig : ApiConfig
{
    public FactorConfig[] Factors { get; init; } = [];
}