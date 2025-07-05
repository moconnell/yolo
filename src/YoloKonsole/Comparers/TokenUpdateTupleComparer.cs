using System.Collections.Generic;
using YoloAbstractions;

namespace YoloKonsole.Comparers;

public class
    TokenUpdateTupleComparer : IComparer<(IReadOnlyDictionary<string, TradeResult>, IReadOnlyDictionary<string, Position>)>
{
    private readonly ReadOnlyDictionaryComparer<string, TradeResult> _tradeResultsComparer;
    private readonly ReadOnlyDictionaryComparer<string, Position> _positionsComparer;

    public TokenUpdateTupleComparer(ReadOnlyDictionaryComparer<string, TradeResult> tradeResultsComparer, ReadOnlyDictionaryComparer<string, Position> positionsComparer)
    {
        _tradeResultsComparer = tradeResultsComparer;
        _positionsComparer = positionsComparer;
    }
    
    public TokenUpdateTupleComparer() : this(new ReadOnlyDictionaryComparer<string, TradeResult>(new TradeResultComparer()), new ReadOnlyDictionaryComparer<string, Position>(new PositionComparer()))
    {}

    public int Compare(
        (IReadOnlyDictionary<string, TradeResult>, IReadOnlyDictionary<string, Position>) x,
        (IReadOnlyDictionary<string, TradeResult>, IReadOnlyDictionary<string, Position>) y)
    {
        var (tradeResults1, positions1) = x;
        var (tradeResults2, positions2) = y;

        var tradeResultsComparison = _tradeResultsComparer.Compare(tradeResults1, tradeResults2);
        if (tradeResultsComparison != 0)
            return tradeResultsComparison;

        return _positionsComparer.Compare(positions1, positions2);
    }
}