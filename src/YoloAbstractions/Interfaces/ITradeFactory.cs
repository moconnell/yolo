using System.Collections.Generic;

namespace YoloAbstractions.Interfaces;

public interface ITradeFactory
{
    IEnumerable<Trade> CalculateTrades(
        IReadOnlyDictionary<string, Weight> weights,
        IReadOnlyDictionary<string, IReadOnlyList<Position>> positions,
        IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>> markets);
}