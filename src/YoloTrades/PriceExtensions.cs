using System;
using System.Collections.Generic;
using System.Linq;
using YoloAbstractions;

public static class PriceExtensions
{
    public static IEnumerable<MarketInfo> GetMarkets(
        this IDictionary<string, IEnumerable<MarketInfo>> markets,
        string token)
    {
        return markets.TryGetValue(token, out var tokenMarkets)
                    ? tokenMarkets
                    : Array.Empty<MarketInfo>();
    }
}