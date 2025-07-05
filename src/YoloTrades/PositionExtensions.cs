using System.Collections.Generic;
using System.Linq;
using YoloAbstractions;

namespace YoloTrades;

public static class PositionExtensions
{
    private static readonly Dictionary<string, MarketInfo> NoMarkets = new();
    
    public static decimal GetTotalValue(
        this IDictionary<string, IDictionary<string, Position>> positions,
        IDictionary<string, IDictionary<string, MarketInfo>> markets,
        string baseCurrencyToken)
    {
        decimal PositionValue(KeyValuePair<string, IDictionary<string, Position>> kvp)
        {
            var (_, tokenPositions) = kvp;
            return tokenPositions.Values.Sum(p => p.GetValue(markets, baseCurrencyToken));
        }

        return positions.Sum(PositionValue);
    }

    private static decimal GetValue(
        this Position position,
        IDictionary<string, IDictionary<string, MarketInfo>> markets,
        string baseCurrencyToken)
    {
        var (assetName, baseAsset, _, amount) = position;
        if (baseAsset == baseCurrencyToken)
            return position.Amount;
        
        var tokenMarkets = markets.GetValueOrDefault(baseAsset, NoMarkets);
        if (tokenMarkets.TryGetValue(assetName, out var positionMarket))
        {
            return amount * positionMarket.Bid.GetValueOrDefault();
        }

        return 0;
    }
}