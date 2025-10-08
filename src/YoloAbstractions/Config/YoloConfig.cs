using System.Collections.Generic;

namespace YoloAbstractions.Config;

public record YoloConfig
{
    public decimal MaxLeverage { get; init; } = 2;
    public decimal TradeBuffer { get; init; }
    public decimal? NominalCash { get; init; }
    public AssetPermissions AssetPermissions { get; init; } = AssetPermissions.SpotAndPerp;
    public required string BaseAsset { get; init; }
    public decimal SpreadSplit { get; init; } = 0.5m;
    public decimal? MinOrderValue { get; init; }
    public string UnfilledOrderTimeout { get; init; } = "00:05:00"; // Default 5 minutes
    public decimal MaxWeightingAbs { get; init; } = 0.25m;
    public IReadOnlyDictionary<FactorType, decimal> FactorWeights { get; init; } = new Dictionary<FactorType, decimal>();
}
