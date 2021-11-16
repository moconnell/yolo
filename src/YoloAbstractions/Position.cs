namespace YoloAbstractions
{
    public record Position(
        string AssetName,
        AssetType AssetType,
        decimal Amount)
    {
        public static readonly Position Null = new(string.Empty, AssetType.Spot, 0);
    }
}