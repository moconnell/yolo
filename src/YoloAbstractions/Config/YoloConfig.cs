namespace YoloAbstractions.Config;

public record YoloConfig
{
    public decimal MaxLeverage { get; init; } = 1;
    public decimal TradeBuffer { get; init; }
    public RebalanceMode RebalanceMode { get; init; } = RebalanceMode.Center;
    public decimal? NominalCash { get; init; }
    public AssetPermissions AssetPermissions { get; init; } = AssetPermissions.PerpetualFutures;
    public string BaseAsset { get; init; } = "USDC";
    public decimal SpreadSplit { get; init; } = 0.5m;
    public decimal? MinOrderValue { get; init; } = 10;
    public bool KillOpenOrders { get; init; } = false;
    public string UnfilledOrderTimeout { get; init; } = "00:05:00"; // Default 5 minutes
    public double? MaxWeightingAbs { get; init; }
    public IReadOnlyDictionary<FactorType, decimal> FactorWeights { get; init; } = new Dictionary<FactorType, decimal>();
    public NormalizationMethod NormalizationMethod { get; init; } = NormalizationMethod.None;
    public int? QuantilesForNormalization { get; init; }
}
