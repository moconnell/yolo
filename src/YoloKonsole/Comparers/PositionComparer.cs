using System;
using System.Collections.Generic;
using YoloAbstractions;

namespace YoloKonsole.Comparers;

public class PositionComparer : IComparer<Position>
{
    public int Compare(Position? x, Position? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (ReferenceEquals(null, y))
        {
            return 1;
        }

        if (ReferenceEquals(null, x))
        {
            return -1;
        }

        var assetNameComparison = string.Compare(x.AssetName, y.AssetName, StringComparison.Ordinal);
        if (assetNameComparison != 0)
        {
            return assetNameComparison;
        }

        var baseAssetComparison = string.Compare(x.BaseAsset, y.BaseAsset, StringComparison.Ordinal);
        if (baseAssetComparison != 0)
        {
            return baseAssetComparison;
        }

        var assetTypeComparison = x.AssetType.CompareTo(y.AssetType);
        if (assetTypeComparison != 0)
        {
            return assetTypeComparison;
        }

        return x.Amount.CompareTo(y.Amount);
    }
}