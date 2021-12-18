using System.Collections.Generic;
using System.Linq;
using YoloAbstractions;

namespace YoloTrades;

public static class PositionExtensions
{
    public static decimal GetTotalValue(
        this IDictionary<string, IEnumerable<Position>> positions,
        IDictionary<string, IEnumerable<MarketInfo>> markets,
        string baseCurrencyToken)
    {
        decimal PositionValue(KeyValuePair<string, IEnumerable<Position>> kvp)
        {
            var (symbol, position) = kvp;

            return baseCurrencyToken == symbol
                ? position.Sum(p => p.Amount)
                : position.Sum(p => p.GetValue(markets, baseCurrencyToken));
        }

        return positions.Sum(PositionValue);
    }

    private static decimal GetValue(
        this Position position,
        IDictionary<string, IEnumerable<MarketInfo>> markets,
        string baseCurrencyToken)
    {
        var (_, assetUnderlying, _, amount) = position;

        return markets
            .GetMarkets(assetUnderlying)
            .Select(market => amount * market.Bid.GetValueOrDefault())
            .FirstOrDefault();
    }
}