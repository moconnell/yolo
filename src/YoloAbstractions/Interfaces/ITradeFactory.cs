using System.Collections.Generic;

namespace YoloAbstractions.Interfaces;

public interface ITradeFactory
{
    IEnumerable<Trade> CalculateTrades(
        IReadOnlyDictionary<string, IReadOnlyDictionary<FactorType, Factor>> factors,
        IReadOnlyDictionary<string, IReadOnlyList<Position>> positions,
        IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>> markets);
}