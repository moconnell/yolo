namespace YoloAbstractions.Config;

public class YoloConfig
{
    public string WeightsUrl { get; init; }
    public string DateFormat { get; init; } = "yyyy-MM-dd";
    public decimal MaxLeverage { get; init; } = 1;
    public decimal TradeBuffer { get; init; }
    public decimal? NominalCash { get; init; }
    public AssetPermissions AssetPermissions { get; init; } = AssetPermissions.SpotAndPerp;
    public string BaseAsset { get; init; }
    public decimal SpreadSplit { get; init; } = 0.5m;
}