using System;
using System.Collections.Generic;
using YoloAbstractions;

namespace YoloKonsole.Comparers;

public class OrderComparer : IComparer<Order>
{
    public int Compare(Order? x, Order? y)
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

        var createdComparison = x.Created.CompareTo(y.Created);
        if (createdComparison != 0)
        {
            return createdComparison;
        }

        var orderSideComparison = x.OrderSide.CompareTo(y.OrderSide);
        if (orderSideComparison != 0)
        {
            return orderSideComparison;
        }

        var orderStatusComparison = x.OrderStatus.CompareTo(y.OrderStatus);
        if (orderStatusComparison != 0)
        {
            return orderStatusComparison;
        }

        var amountComparison = x.Amount.CompareTo(y.Amount);
        if (amountComparison != 0)
        {
            return amountComparison;
        }

        var amountRemainingComparison = x.AmountRemaining.CompareTo(y.AmountRemaining);
        if (amountRemainingComparison != 0)
        {
            return amountRemainingComparison;
        }

        var limitPriceComparison = Nullable.Compare(x.LimitPrice, y.LimitPrice);
        if (limitPriceComparison != 0)
        {
            return limitPriceComparison;
        }

        return string.Compare(x.ClientId, y.ClientId, StringComparison.Ordinal);
    }
}