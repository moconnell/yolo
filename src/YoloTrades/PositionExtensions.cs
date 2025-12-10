using YoloAbstractions;

namespace YoloTrades;

public static class PositionExtensions
{
    public static decimal GetTotalValue(
        this IReadOnlyDictionary<string, IReadOnlyList<Position>> positions,
        IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>> markets,
        string baseCurrencyToken)
    {
        return positions.Sum(PositionValue);

        decimal PositionValue(KeyValuePair<string, IReadOnlyList<Position>> kvp)
        {
            var (symbol, position) = kvp;

            return baseCurrencyToken == symbol
                ? position.Sum(p => p.Amount)
                : position.Sum(p => p.GetValue(markets, baseCurrencyToken));
        }
    }

    private static decimal GetValue(
        this Position position,
        IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>> markets,
        string baseCurrencyToken)
    {
        var (_, assetUnderlying, _, amount) = position;

        var tokenMarkets = markets.GetMarkets(assetUnderlying);

        var isLong = amount >= 0;
        var sidePricesInBase = tokenMarkets
            .Where(m => string.Equals(m.QuoteAsset, baseCurrencyToken, System.StringComparison.OrdinalIgnoreCase))
            .Select(m => isLong ? m.Bid : m.Ask)
            .Where(p => p.HasValue)
            .Select(p => p!.Value);

        var price = isLong
            ? sidePricesInBase.DefaultIfEmpty(0m).Max()
            : sidePricesInBase.DefaultIfEmpty(0m).Min();

        return amount * price;
    }
}