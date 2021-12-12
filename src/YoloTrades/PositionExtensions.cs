using System.Collections.Generic;
using System.Linq;
using YoloAbstractions;

namespace YoloTrades;

public static class PositionExtensions
{
    public static decimal GetTotalValue(
        this IDictionary<string, Position> positions,
        IDictionary<string, IEnumerable<MarketInfo>> markets,
        string baseCurrencyToken)
    {
        decimal PositionValue(KeyValuePair<string, Position> kvp)
        {
            var (symbol, position) = kvp;

            return baseCurrencyToken == symbol
                ? position.Amount
                : position.GetValue(markets, baseCurrencyToken);
        }

        return positions.Sum(PositionValue);
    }

    private static decimal GetValue(
        this Position position,
        IDictionary<string, IEnumerable<MarketInfo>> markets,
        string baseCurrencyToken)
    {
        var (assetName, assetUnderlying, assetType, amount) = position;

        var symbol = assetType switch
        {
            AssetType.Spot => $"{assetName}{baseCurrencyToken}",
            _ => assetName
        };

        return markets
            .GetMarkets(assetUnderlying, assetType)
            .Select(market => amount * market.Bid.GetValueOrDefault())
            .FirstOrDefault();
    }
}