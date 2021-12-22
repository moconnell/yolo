using System;

namespace YoloAbstractions;

public record Trade(
    string AssetName,
    AssetType AssetType,
    decimal Amount,
    decimal? LimitPrice = null,
    bool? PostPrice = null,
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

        var totalAmount = one.Amount + two.Amount;
        var postPrice = one.PostPrice == true && Math.Abs(totalAmount) >= Math.Abs(one.Amount) ||
                       two.PostPrice == true && Math.Abs(totalAmount) >= Math.Abs(two.Amount);

        return new Trade(
            one.AssetName,
            one.AssetType,
            totalAmount,
            one.LimitPrice,
            postPrice,
            one.Expiry);
    }
}