using System.Collections.Generic;
using YoloAbstractions;

namespace YoloTrades;

public interface ITradeFactory
{
    IEnumerable<Trade> CalculateTrades(
        IReadOnlyList<Weight> weights,
        IDictionary<string, IReadOnlyList<Position>> positions,
        IDictionary<string, IReadOnlyList<MarketInfo>> markets);
}