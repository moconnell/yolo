using System;

namespace YoloAbstractions
{
    public record Price(
        string AssetName,
        AssetType AssetType,
        decimal Last,
        DateTime? At);
}