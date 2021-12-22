using System;

namespace YoloAbstractions;

public record Trade(
    string AssetName,
    AssetType AssetType,
    decimal Amount,
    decimal? LimitPrice = null,
    DateTime? Expiry = null)
{
    public bool IsTradable => Amount != 0;

    public static Trade operator +(Trade one, Trade two)
    {
        if (one.AssetName != two.AssetName || one.AssetType != two.AssetType)
            throw new ArgumentException("Cannot add trades for different instruments");
        
        if (one.LimitPrice != two.LimitPrice)
            throw new ArgumentException("Cannot add trades with different limit price");
        
        if (one.Expiry != two.Expiry)
            throw new ArgumentException("Cannot add trades with different expiry");

        return new Trade(one.AssetName, one.AssetType, one.Amount + two.Amount, one.LimitPrice, one.Expiry);
    }
}