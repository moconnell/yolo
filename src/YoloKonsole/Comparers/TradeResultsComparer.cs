using System.Collections.Generic;
using System.Linq;
using YoloAbstractions;

namespace YoloKonsole.Comparers;

public class TradeResultsComparer : IComparer<IEnumerable<TradeResult>>
{
    private readonly IComparer<TradeResult> _tradeResultComparer;

    public TradeResultsComparer() : this(new TradeResultComparer())
    {
    }

    private TradeResultsComparer(IComparer<TradeResult> tradeResultComparer) =>
        _tradeResultComparer = tradeResultComparer;

    public int Compare(
        IEnumerable<TradeResult>? x,
        IEnumerable<TradeResult>? y)
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

        var xResults = x.ToArray();
        var yResults = y.ToArray();
        if (xResults.Length < yResults.Length)
            return 1;
        if (xResults.Length > yResults.Length)
            return -1;

        for (var i = 0; i < xResults.Length; i++)
        {
            var tradeResultComparison = _tradeResultComparer.Compare(xResults[i], yResults[i]);
            if (tradeResultComparison != 0)
                return tradeResultComparison;
        }

        return 0;
    }
}