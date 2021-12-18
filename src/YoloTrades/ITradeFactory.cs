using System.Collections.Generic;
using YoloAbstractions;

namespace YoloTrades;

public interface ITradeFactory
{
    IEnumerable<Trade> CalculateTrades(
        IEnumerable<Weight> weights,
        IDictionary<string, IEnumerable<Position>> positions,
        IDictionary<string, IEnumerable<MarketInfo>> markets);
}