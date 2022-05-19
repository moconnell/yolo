using System.Collections.Generic;
using YoloAbstractions;

namespace YoloTrades;

public interface ITradeFactory
{
    IEnumerable<Trade> CalculateTrades(
        IDictionary<string, Weight> weights,
        IDictionary<string, IEnumerable<Position>> positions,
        IDictionary<string, IEnumerable<MarketInfo>> markets);
}