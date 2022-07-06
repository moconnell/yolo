using System;
using System.Collections.Generic;
using YoloAbstractions;

namespace YoloKonsole.Comparers;

public class TradeResultComparer : IComparer<TradeResult>
{
    private readonly IComparer<Trade> _tradeComparer;
    private readonly IComparer<Order> _orderComparer;

    public TradeResultComparer() : this(new TradeComparer(), new OrderComparer())
    {
    }

    public TradeResultComparer(IComparer<Trade> tradeComparer, IComparer<Order> orderComparer)
    {
        _tradeComparer = tradeComparer;
        _orderComparer = orderComparer;
    }

    public int Compare(TradeResult? x, TradeResult? y)
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

        var successComparison = Nullable.Compare(x.Success, y.Success);
        if (successComparison != 0)
        {
            return successComparison;
        }

        var errorComparison = string.Compare(x.Error, y.Error, StringComparison.Ordinal);
        if (errorComparison != 0)
        {
            return errorComparison;
        }

        var errorCodeComparison = Nullable.Compare(x.ErrorCode, y.ErrorCode);
        if (errorCodeComparison != 0)
            return errorCodeComparison;

        var tradeComparison = _tradeComparer.Compare(x.Trade, y.Trade);
        if (tradeComparison != 0)
            return tradeComparison;

        return _orderComparer.Compare(x.Order, y.Order);
    }
}