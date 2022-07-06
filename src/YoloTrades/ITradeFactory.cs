using System.Collections.Generic;
using System.Linq;
using YoloAbstractions;

namespace YoloTrades;

public interface ITradeFactory
{
    IEnumerable<IGrouping<string, Trade>> CalculateTrades(
        IDictionary<string, Weight> weights,
        IDictionary<string, IEnumerable<Position>> positions,
        IDictionary<string, IEnumerable<MarketInfo>> markets);
}