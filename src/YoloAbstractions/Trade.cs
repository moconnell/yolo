using System;

namespace YoloAbstractions;

public record Trade(
    string AssetName,
    AssetType AssetType,
    decimal Amount,
    decimal? LimitPrice = null,
    bool? PostPrice = null,
    DateTime? Expiry = null,
    string? ClientOrderId = null)
{
    public bool IsTradable(decimal? minOrderValue = null)
    {
        if (Amount == 0)
            return false;

        if (string.IsNullOrWhiteSpace(AssetName))
            return false;

        // Check valid limit price
        if (LimitPrice.HasValue && LimitPrice.Value < 0)
            return false;

        // Ensure the order value meets the minimum requirement
        return !minOrderValue.HasValue || !LimitPrice.HasValue || !(AbsoluteAmount * LimitPrice.Value < minOrderValue);
    }

    public decimal AbsoluteAmount => Math.Abs(Amount);

    public OrderSide OrderSide => Amount < 0 ? OrderSide.Sell : OrderSide.Buy;

    public OrderType OrderType => LimitPrice.HasValue ? OrderType.Limit : OrderType.Market;

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