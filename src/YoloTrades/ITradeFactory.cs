using System.Collections.Generic;
using YoloAbstractions;

namespace YoloTrades;

public interface ITradeFactory
{
    IEnumerable<Trade> CalculateTrades(
        IReadOnlyList<Weight> weights,
        IReadOnlyDictionary<string, IReadOnlyList<Position>> positions,
        IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>> markets);
}