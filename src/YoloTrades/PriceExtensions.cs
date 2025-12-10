using YoloAbstractions;

namespace YoloTrades;

public static class PriceExtensions
{
    public static IReadOnlyList<MarketInfo> GetMarkets(
        this IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>> markets,
        string token) =>
        markets.TryGetValue(token, out var tokenMarkets)
            ? tokenMarkets
            : [];
}