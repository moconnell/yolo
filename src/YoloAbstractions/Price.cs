using System;

namespace YoloAbstractions
{
    public record Price(
        string AssetName,
        AssetType AssetType,
        decimal? Ask,
        decimal? Bid,
        decimal? Last,
        DateTime? At);
}