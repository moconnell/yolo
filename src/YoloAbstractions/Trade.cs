namespace YoloAbstractions;

public record Trade(
    string Symbol,
    AssetType AssetType,
    decimal Amount,
    decimal? LimitPrice = null,
    OrderType OrderType = OrderType.Limit,
    bool? PostPrice = null,
    bool? ReduceOnly = null,
    DateTime? Expiry = null,
    string? ClientOrderId = null)
{
    public bool IsTradable(decimal? minOrderValue = null)
    {
        if (Amount == 0)
            return false;

        if (string.IsNullOrWhiteSpace(Symbol))
            return false;

        if (OrderType == OrderType.Limit && !LimitPrice.HasValue)
            return false;

        // Check valid limit price
        if (LimitPrice.HasValue && LimitPrice.Value < 0)
            return false;

        // Ensure the order value meets the minimum requirement
        return ReduceOnly == true || !minOrderValue.HasValue || !LimitPrice.HasValue || !(AbsoluteAmount * LimitPrice.Value < minOrderValue);
    }

    public decimal AbsoluteAmount => Math.Abs(Amount);

    public OrderSide OrderSide => Amount < 0 ? OrderSide.Sell : OrderSide.Buy;

    public static Trade operator +(Trade one, Trade two)
    {
        if (one.Symbol != two.Symbol || one.AssetType != two.AssetType)
            throw new ArgumentException("Cannot add trades for different instruments");

        if (one.LimitPrice != two.LimitPrice)
            throw new ArgumentException("Cannot add trades with different limit price");

        if (one.Expiry != two.Expiry)
            throw new ArgumentException("Cannot add trades with different expiry");

        var totalAmount = one.Amount + two.Amount;
        var postPrice = one.PostPrice == true && Math.Abs(totalAmount) >= Math.Abs(one.Amount) ||
                       two.PostPrice == true && Math.Abs(totalAmount) >= Math.Abs(two.Amount);
        bool? reduceOnly = one.ReduceOnly switch
        {
            true => two.ReduceOnly == true,
            false => false,
            null => null
        };

        return new Trade(
            one.Symbol,
            one.AssetType,
            totalAmount,
            one.LimitPrice,
            one.OrderType,
            postPrice,
            reduceOnly,
            one.Expiry);
    }
}