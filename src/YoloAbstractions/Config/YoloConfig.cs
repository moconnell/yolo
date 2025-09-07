namespace YoloAbstractions.Config;

public class YoloConfig
{
    public string ApiBaseUrl { get; init; } = "";
    public string ApiKey { get; init; } = "";
    public string VolatilitiesUrlPath { get; init; } = "volatilities";
    public string WeightsUrlPath { get; init; } = "weights";
    public string DateFormat { get; init; } = "yyyy-MM-dd";
    public decimal MaxLeverage { get; init; } = 2;
    public decimal TradeBuffer { get; init; }
    public decimal? NominalCash { get; init; }
    public AssetPermissions AssetPermissions { get; init; } = AssetPermissions.SpotAndPerp;
    public required string BaseAsset { get; init; }
    public decimal SpreadSplit { get; init; } = 0.5m;
    public decimal? MinOrderValue { get; init; }
    public string UnfilledOrderTimeout { get; init; } = "00:05:00"; // Default 5 minutes
    public decimal WeightingCarryFactor { get; init; } = 1m;
    public decimal WeightingMomentumFactor { get; init; } = 1m;
    public decimal WeightingTrendFactor { get; init; } = 1m;
    public decimal MaxWeightingAbs { get; init; } = 0.25m;
}