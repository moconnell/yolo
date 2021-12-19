using System;

namespace YoloAbstractions;

public record Trade(
    string AssetName,
    AssetType AssetType,
    decimal Amount,
    decimal? LimitPrice = null,
    DateTime? Expiry = null)
{
    public bool IsTradeable => Amount != 0;
}