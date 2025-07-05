using System.Collections.Generic;
using System.Linq;
using YoloAbstractions;

namespace YoloTrades;

public interface ITradeFactory
{
    IEnumerable<IGrouping<string, Trade>> CalculateTrades(
        IDictionary<string, Weight> weights,
        IDictionary<string, IDictionary<string, Position>> positions,
        IDictionary<string, IDictionary<string, MarketInfo>> markets);
}