using System;
using System.Collections.Generic;
using YoloAbstractions;

namespace YoloKonsole.Comparers;

public class TradeComparer : IComparer<Trade>
{
    public int Compare(Trade? x, Trade? y)
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

        var idComparison = x.Id.CompareTo(y.Id);
        if (idComparison != 0)
        {
            return idComparison;
        }

        var assetNameComparison = string.Compare(x.AssetName, y.AssetName, StringComparison.Ordinal);
        if (assetNameComparison != 0)
        {
            return assetNameComparison;
        }

        var assetTypeComparison = x.AssetType.CompareTo(y.AssetType);
        if (assetTypeComparison != 0)
        {
            return assetTypeComparison;
        }

        var baseAssetComparison = string.Compare(x.BaseAsset, y.BaseAsset, StringComparison.Ordinal);
        if (baseAssetComparison != 0)
        {
            return baseAssetComparison;
        }

        var amountComparison = x.Amount.CompareTo(y.Amount);
        if (amountComparison != 0)
        {
            return amountComparison;
        }

        var limitPriceComparison = Nullable.Compare(x.LimitPrice, y.LimitPrice);
        if (limitPriceComparison != 0)
        {
            return limitPriceComparison;
        }

        var postPriceComparison = Nullable.Compare(x.PostPrice, y.PostPrice);
        if (postPriceComparison != 0)
        {
            return postPriceComparison;
        }

        return Nullable.Compare(x.Expiry, y.Expiry);
    }
}