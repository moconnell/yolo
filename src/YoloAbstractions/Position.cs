namespace YoloAbstractions;

public record Position(
    string AssetName,
    string BaseAsset,
    AssetType AssetType,
    decimal Amount)
{
    public static readonly Position Null = new(string.Empty, string.Empty, AssetType.Spot, 0);
}